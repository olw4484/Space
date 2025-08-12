/// <summary>
/// ����Ƽ �����Ϳ��� Ŀ���� ��ũ��Ʈ ���ø��� �������
/// MonoBehaviour �Ǵ� ScriptableObject ��ũ��Ʈ�� �����ϰ� �����ϴ� ��ƿ Ŭ����.
/// </summary>
/// <remarks>
/// - ���ø� ����(.cs.txt)�� �ݵ�� <c>Assets/Editor/ScriptTemplates</c> ������ ��ġ�ؾ� �մϴ�.
/// - <c>#SCRIPTNAME#</c> �� <c>#ROOTNAMESPACE#</c> ��ū�� ġȯ�մϴ�.
/// - ���ӽ����̽� ���� �켱����: ���� ����� asmdef�� <c>rootNamespace</c> �� Project Settings �� <c>DEFAULT_NAMESPACE</c>.
/// - <c>ProjectWindowUtil.StartNameEditingIfProjectWindowExists</c> ��� �� �̸� ���� �ݹ��� ���� ���� ���� Ŭ������ �����մϴ�.
/// </remarks>
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

public static class SafeScriptCreator
{
    /// <summary>
    /// ������Ʈ ���� ���ø� ���� ���.
    /// </summary>
    private const string TEMPLATE_DIR = "Assets/Editor/ScriptTemplates";

    /// <summary>
    /// MonoBehaviour ���ø� ���� ���(.cs.txt).
    /// </summary>
    private const string MONO_TEMPLATE = TEMPLATE_DIR + "/MonoBehaviourTemplate.cs.txt";

    /// <summary>
    /// ScriptableObject ���ø� ���� ���(.cs.txt).
    /// </summary>
    private const string SO_TEMPLATE = TEMPLATE_DIR + "/ScriptableObjectTemplate.cs.txt";

    /// <summary>
    /// rootNamespace�� ������� �� ����� �⺻ ���ӽ����̽�.
    /// </summary>
    private const string DEFAULT_NAMESPACE = "DefaultNamespace";

    // =============== �޴� ===============

    /// <summary>
    /// MonoBehaviour ���ø����� �� ��ũ��Ʈ�� �����մϴ�.
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/MonoBehaviour (safe)", false, 1)]
    public static void CreateMono()
        => CreateFromTemplate(MONO_TEMPLATE, "NewMonoBehaviour.cs");

    /// <summary>
    /// ScriptableObject ���ø����� �� ��ũ��Ʈ�� �����մϴ�.
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/ScriptableObject (safe)", false, 2)]
    public static void CreateSO()
        => CreateFromTemplate(SO_TEMPLATE, "NewScriptableObject.cs");

    // =============== ��ü ===============

    /// <summary>
    /// ���ø��� �о� Ŭ������/���ӽ����̽� ��ū�� ġȯ�� �� �� ��ũ��Ʈ�� �����մϴ�.
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

        // ���� ��ġ/�̸�
        string targetDir = GetSelectedPathOrAssets();
        string outPath = Path.Combine(targetDir, defaultName).Replace("\\", "/");
        string className = Path.GetFileNameWithoutExtension(outPath);

        // ���ø� �ε� & ġȯ
        string txt = File.ReadAllText(templatePath);
        string rootNs = ResolveRootNamespace(targetDir);

        txt = txt.Replace("#SCRIPTNAME#", className)
                 .Replace("#ROOTNAMESPACE#", rootNs);

        File.WriteAllText(outPath, txt);
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outPath);

        // �̸� ��� ���� ��� ������ �ʿ��� ��:
        // ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
        //     0, ScriptableObject.CreateInstance<DoNothingAction>(), outPath, null, null
        // );
    }

    /// <summary>
    /// ���� ���õ� ������ ������� �� ��θ�, �����̶�� �θ� ���� ��θ� ��ȯ�մϴ�.
    /// ������ ������ <c>Assets</c>�� ��ȯ�մϴ�.
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
    /// ���ӽ����̽��� �����մϴ�. ����� asmdef�� <c>rootNamespace</c>�� ������ �켱 ����ϰ�,
    /// ������ Project Settings�� Root Namespace�� ����ϸ�, �� �� ���ų� ��ȿ�� �⺻���� ��ȯ�մϴ�.
    /// </summary>
    /// <param name="folder">���� ��� ���� ���</param>
    private static string ResolveRootNamespace(string folder)
    {
        // 1) ����� asmdef
        var asmdefPath = FindNearestAsmdef(folder);
        if (!string.IsNullOrEmpty(asmdefPath))
        {
            var rn = ReadAsmdefRootNamespace(asmdefPath);
            if (IsValidNamespace(rn)) return rn;
        }

        // 2) Project Settings ��
        var projectNs = EditorSettings.projectGenerationRootNamespace; // 2022/6000 ����
        if (IsValidNamespace(projectNs)) return projectNs;

        // 3) �⺻��
        return DEFAULT_NAMESPACE;
    }

    /// <summary>
    /// �־��� �������� ������ �Ž��� �ö󰡸� ���� ����� <c>.asmdef</c> ������ ã���ϴ�.
    /// </summary>
    /// <param name="folder">�˻� ���� ����</param>
    /// <returns>asmdef ���� ��� �Ǵ� null</returns>
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

            // Assets ��Ʈ ������ ������ �ߴ�
            if (!parent.FullName.Replace("\\", "/").Contains("/Assets")) break;
            folder = parent.FullName.Replace("\\", "/");
        }
        return null;
    }

    /// <summary>
    /// asmdef JSON���� <c>rootNamespace</c> ���� �о�ɴϴ�.
    /// �Ľ� ���� �� �� ���ڿ��� ��ȯ�մϴ�.
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
    /// C# ���ӽ����̽��μ� ��ȿ���� ���� ��Ģ���� �˻��մϴ�.
    /// ����/���� ����, ���� ����/����/���� ���, <c>.</c> ���� ���.
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
    /// �̸� ���� �� �߰� �۾��� ���� �ʿ��� ���� �ݹ� Ŭ�����Դϴ�.
    /// ����� ����� ������, �ʿ� �� <see cref="Action"/> ���ο� ��ó���� �����ϼ���.
    /// </summary>
    private class DoNothingAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        /// <summary>
        /// �̸� ���� �Ϸ� �� ȣ��˴ϴ�. ����� �α׸� ����մϴ�.
        /// </summary>
        /// <param name="instanceId">�ν��Ͻ� ID</param>
        /// <param name="pathName">���� ���(���ϸ� ����)</param>
        /// <param name="resourceFile">���ҽ� ���� ���(���ø�)</param>
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string newName = Path.GetFileNameWithoutExtension(pathName);
            Debug.Log($"�� ���� �̸�: {newName}");
        }
    }

    /// <summary>
    /// asmdef JSON ������ȭ�� ���� �ּ� ����ü.
    /// </summary>
    [Serializable]
    private class AsmdefJson
    {
        /// <summary>asmdef�� rootNamespace ��.</summary>
        public string rootNamespace;
    }
}
#endif
