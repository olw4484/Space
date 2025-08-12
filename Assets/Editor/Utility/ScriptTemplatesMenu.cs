#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ����Ƽ �����Ϳ��� Ŀ���� ��ũ��Ʈ ���ø��� �������
/// MonoBehaviour, ScriptableObject, Partial Ŭ���� ������ �����ϰ� �����ϴ� ��ƿ Ŭ����.
/// </summary>
/// <remarks>
/// - ���ø� ����(.cs.txt)�� �ݵ�� <c>Assets/Editor/ScriptTemplates</c> ������ ��ġ�ؾ� �մϴ�.
/// - <c>#SCRIPTNAME#</c> �� <c>#ROOTNAMESPACE#</c> ��ū�� ġȯ�մϴ�.
/// - ���ӽ����̽� ���� �켱����:
///   1) ���� ����� asmdef�� rootNamespace
///   2) Project Settings �� Root Namespace
///   3) DEFAULT_NAMESPACE ���
/// </remarks>
public static class SafeScriptCreator
{
    /// <summary>������Ʈ ���� ���ø� ���� ���.</summary>
    private const string TEMPLATE_DIR = "Assets/Editor/ScriptTemplates";

    /// <summary>MonoBehaviour ���ø� ���� ���(.cs.txt).</summary>
    private const string MONO_TEMPLATE = TEMPLATE_DIR + "/MonoBehaviourTemplate.cs.txt";

    /// <summary>MonoTemplate ���ø� ���� ���(.cs.txt).</summary>
    private const string MONO_PARTIAL_TEMPLATE = TEMPLATE_DIR + "/MonoTemplate.Editor.cs.txt";         // ������ ���� partial

    /// <summary>ScriptableObject ���ø� ���� ���(.cs.txt).</summary>
    private const string SO_TEMPLATE = TEMPLATE_DIR + "/ScriptableObjectTemplate.cs.txt";

    /// <summary>rootNamespace�� ������� �� ����� �⺻ ���ӽ����̽�.</summary>
    private const string DEFAULT_NAMESPACE = "DefaultNamespace";

    /// <summary>EditorTestConfig ���ø� ���� ���(.cs.txt).</summary>
    private const string EDITOR_CONFIG_PATH = "Assets/Editor/EditorTestConfig.cs";

    /// <summary>EditorTestConfig ���ø� (UTF-8 BOM�� ���� ����)</summary>
    private const string EDITOR_CONFIG_TEMPLATE = @"#if UNITY_EDITOR
namespace #ROOTNAMESPACE#
{
    /// <summary>
    /// ������ ���� �׽�Ʈ �ڵ���� ���� On/Off ����ġ.
    /// </summary>
    public static class EditorTestConfig
    {
        /// <summary>true�� ������ �׽�Ʈ �ڵ� ����.</summary>
        public static bool Enabled = true;
    }
}
#endif
";

    // ======================= �޴� =======================

    /// <summary>
    /// EditorTestConfig ��ũ��Ʈ�� �����մϴ�(���� 1ȸ). 
    /// Assets/Editor/EditorTestConfig.cs �� �����Ǹ�, UTF-8 BOM���� ����˴ϴ�.
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/Create EditorTestConfig", false, 50)]
    public static void CreateEditorTestConfig()
    {
        // ���� Project â���� ������ ���� ���
        string targetDir = GetSelectedPathOrAssets();
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        string configPath = Path.Combine(targetDir, "EditorTestConfig.cs").Replace("\\", "/");

        if (File.Exists(configPath))
        {
            EditorUtility.DisplayDialog("Already Exists", "�̹� EditorTestConfig.cs�� �����մϴ�.", "OK");
            return;
        }

        string ns = ResolveRootNamespace(targetDir);
        if (string.IsNullOrWhiteSpace(ns)) ns = DEFAULT_NAMESPACE;

        var content = EDITOR_CONFIG_TEMPLATE.Replace("#ROOTNAMESPACE#", ns);
        WriteAllTextUtf8Bom(configPath, content);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", $"EditorTestConfig.cs ���� �Ϸ�!\n{configPath}", "OK");
        Debug.Log($"[Create] {configPath}");
    }

    /// <summary>
    /// MonoBehaviour ���ø����� �� ��ũ��Ʈ�� �����մϴ�.
    /// (�̸� ���� �Ϸ� ��, �� Ŭ������ Editor partial�� �Բ� ����)
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/MonoBehaviour (safe)", false, 1)]
    public static void CreateMono()
    {
        string targetDir = GetSelectedPathOrAssets();
        string suggestedPath = Path.Combine(targetDir, "NewMonoBehaviour.cs").Replace("\\", "/");

        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<CreateMonoWithPartialAction>(),
            suggestedPath,
            null,
            MONO_TEMPLATE // ����� ����(�ʼ��� �ƴ�)
        );
    }

    /// <summary>
    /// ScriptableObject ���ø����� �� ��ũ��Ʈ�� �����մϴ�.
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/ScriptableObject (safe)", false, 2)]
    public static void CreateSO()
        => CreateFromTemplate(SO_TEMPLATE, "NewScriptableObject.cs");

    // ======================= ��ü =======================

    /// <summary>
    /// ������ ���ø� ������ �о� Ŭ������/���ӽ����̽��� ġȯ�� ��,
    /// ������ ��ο� �� C# ��ũ��Ʈ�� �����մϴ�.
    /// </summary>
    /// <param name="templatePath">���ø� ���� ���(.cs.txt)</param>
    /// <param name="defaultName">�⺻ ���� ���ϸ�(Ȯ���� ����)</param>
    private static void CreateFromTemplate(string templatePath, string defaultName)
    {
        if (!File.Exists(templatePath))
        {
            EditorUtility.DisplayDialog(
                "Template Missing",
                $"���ø��� ã�� �� �����ϴ�.\n{templatePath}\n\n���/���ϸ��� Ȯ���ϼ���(.cs.txt �ʿ�).",
                "OK"
            );
            return;
        }

        // ���� ��ġ/�̸� ����
        string targetDir = GetSelectedPathOrAssets();
        string outPath = Path.Combine(targetDir, defaultName).Replace("\\", "/");
        string className = Path.GetFileNameWithoutExtension(outPath);

        // ���ø� �ε� �� ��ū ġȯ
        string txt = File.ReadAllText(templatePath);
        string rootNs = ResolveRootNamespace(targetDir);

        txt = txt.Replace("#SCRIPTNAME#", className)
                 .Replace("#ROOTNAMESPACE#", rootNs);

        File.WriteAllText(outPath, txt);
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outPath);
    }

    /// <summary>
    /// ���� ���õ� ��θ� ��ȯ�մϴ�.
    /// - ������ ������ ��� �ش� ���� ��θ� ��ȯ
    /// - ������ ������ ��� �ش� ������ �θ� ���� ��θ� ��ȯ
    /// - ������ ���� ��� "Assets"�� ��ȯ
    /// </summary>
    private static string GetSelectedPathOrAssets()
    {
        string path = "Assets";
        foreach (var obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
        {
            var p = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(p)) continue;

            if (File.Exists(p)) return Path.GetDirectoryName(p).Replace("\\", "/");
            if (Directory.Exists(p)) path = p;
        }
        return path;
    }

    /// <summary>
    /// ������ ������ �������� ���ӽ����̽��� �����մϴ�.
    /// </summary>
    /// <param name="folder">�˻� ���� ���� ���</param>
    /// <returns>���ӽ����̽� ���ڿ�</returns>
    private static string ResolveRootNamespace(string folder)
    {
        // 1) ����� asmdef
        var asmdefPath = FindNearestAsmdef(folder);
        if (!string.IsNullOrEmpty(asmdefPath))
        {
            var rn = ReadAsmdefRootNamespace(asmdefPath);
            if (IsValidNamespace(rn)) return rn;
        }

        // 2) Project Settings
        var projectNs = EditorSettings.projectGenerationRootNamespace;
        if (IsValidNamespace(projectNs)) return projectNs;

        // 3) �⺻��
        return DEFAULT_NAMESPACE;
    }

    /// <summary>
    /// �־��� �������� ������ Ž���ϸ� ���� ����� .asmdef ���� ��θ� ��ȯ�մϴ�.
    /// </summary>
    private static string FindNearestAsmdef(string folder)
    {
        folder = folder.Replace("\\", "/");
        while (!string.IsNullOrEmpty(folder))
        {
            var paths = Directory.GetFiles(folder, "*.asmdef", SearchOption.TopDirectoryOnly);
            if (paths != null && paths.Length > 0)
                return paths.First().Replace("\\", "/");

            var parent = Directory.GetParent(folder);
            if (parent == null) break;

            if (!parent.FullName.Replace("\\", "/").Contains("/Assets")) break;
            folder = parent.FullName.Replace("\\", "/");
        }
        return null;
    }

    /// <summary>
    /// asmdef JSON���� rootNamespace ���� �о�ɴϴ�.
    /// </summary>
    private static string ReadAsmdefRootNamespace(string asmdefPath)
    {
        try
        {
            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefJson>(json);
            return data?.rootNamespace ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// ���ڿ��� ��ȿ�� C# ���ӽ����̽����� �˻��մϴ�.
    /// </summary>
    private static bool IsValidNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns)) return false;

        var parts = ns.Split('.');
        foreach (var p in parts)
        {
            if (string.IsNullOrWhiteSpace(p)) return false;
            if (!(char.IsLetter(p[0]) || p[0] == '_')) return false;
            for (int i = 1; i < p.Length; i++)
                if (!(char.IsLetterOrDigit(p[i]) || p[i] == '_')) return false;
        }
        return true;
    }

    /// <summary>
    /// MonoBehaviour + partial Editor ��ũ��Ʈ�� ���ÿ� �����մϴ�.
    /// </summary>
    // ======================= �ݹ�: �̸� Ȯ�� �� �� ���� ���� =======================

    private class CreateMonoWithPartialAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            // ���� ���/�̸�
            string basePath = pathName.Replace("\\", "/");                 // ex) Assets/Scripts/Foo.cs
            string dir = Path.GetDirectoryName(basePath).Replace("\\", "/");
            string className = Path.GetFileNameWithoutExtension(basePath);

            // Editor ���� ����
            string editorDir = dir.Replace("\\", "/");

            string editorPath = Path.Combine(editorDir, $"{className}.Editor.cs").Replace("\\", "/");

            // ���ø� ���� Ȯ��
            if (!File.Exists(MONO_TEMPLATE) || !File.Exists(MONO_PARTIAL_TEMPLATE))
            {
                EditorUtility.DisplayDialog("Template Missing",
                    $"���ø��� ã�� �� �����ϴ�.\n{MONO_TEMPLATE}\n{MONO_PARTIAL_TEMPLATE}",
                    "OK");
                return;
            }

            // ����� ����
            if (File.Exists(basePath) || File.Exists(editorPath))
            {
                EditorUtility.DisplayDialog("Already Exists",
                    $"�̹� ���� �̸��� ������ �����մϴ�.\n{basePath}\n{editorPath}",
                    "OK");
                return;
            }

            // ���ӽ����̽� ����
            string rootNs = ResolveRootNamespace(dir);

            // �� Ŭ���� ����
            string baseTxt = File.ReadAllText(MONO_TEMPLATE)
                .Replace("#SCRIPTNAME#", className)
                .Replace("#ROOTNAMESPACE#", rootNs);

            // ������ partial ����
            string editorTxt = File.ReadAllText(MONO_PARTIAL_TEMPLATE)
                .Replace("#SCRIPTNAME#", className)
                .Replace("#ROOTNAMESPACE#", rootNs);

            File.WriteAllText(basePath, baseTxt);
            File.WriteAllText(editorPath, editorTxt);

            AssetDatabase.ImportAsset(basePath);
            AssetDatabase.ImportAsset(editorPath);
            ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(basePath));

            Debug.Log($"[Create] {className}.cs + Editor/{className}.Editor.cs ���� �Ϸ�");
        }
    }

    /// <summary>
    /// �̸� ���� �� �߰� �۾��� ó���ϴ� ���� Ŭ����.
    /// ����� �ܼ��� partial ���� �޼��带 ȣ���մϴ�.
    /// </summary>
    private class DoNothingAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string newName = Path.GetFileNameWithoutExtension(pathName);
            Debug.Log($"�� ���� �̸�: {newName}");
        }
    }

    /// <summary>asmdef JSON ������ȭ�� ���� �ּ� ����ü.</summary>
    [Serializable]
    private class AsmdefJson
    {
        public string rootNamespace;
    }

    /// <summary>
    /// UTF-8 with BOM ���� ����
    /// </summary>
    private static void WriteAllTextUtf8Bom(string path, string text)
    {
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        using (var sw = new StreamWriter(path, false, utf8Bom))
            sw.Write(text);
    }
}
#endif
