#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

public static class SafeScriptCreator
{
    // ������Ʈ ���� ���ø� ��ġ
    private const string TEMPLATE_DIR = "Assets/Editor/ScriptTemplates";
    private const string MONO_TEMPLATE = TEMPLATE_DIR + "/MonoBehaviourTemplate.cs.txt";
    private const string SO_TEMPLATE = TEMPLATE_DIR + "/ScriptableObjectTemplate.cs.txt";

    // Root Namespace ����� �� ����� �⺻��
    private const string DEFAULT_NAMESPACE = "DefaultNamespace";

    // =============== �޴� ===============
    [MenuItem("Assets/Create/CustomScript/MonoBehaviour (safe)", false, 1)]
    public static void CreateMono()
        => CreateFromTemplate(MONO_TEMPLATE, "NewMonoBehaviour.cs");

    [MenuItem("Assets/Create/CustomScript/ScriptableObject (safe)", false, 2)]
    public static void CreateSO()
        => CreateFromTemplate(SO_TEMPLATE, "NewScriptableObject.cs");

    // =============== ��ü ===============
    private static void CreateFromTemplate(string templatePath, string defaultName)
    {
        if (!File.Exists(templatePath))
        {
            EditorUtility.DisplayDialog("Template Missing",
                $"���ø��� ã�� �� �����ϴ�.\n{templatePath}\n\n" +
                "���/���ϸ��� Ȯ���ϼ���(.cs.txt �ʿ�).", "OK");
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
        // �ٷ� �̸� �ٲٱ� ���� �����Ϸ���:
        // ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoNothingAction>(), outPath, null, null);
    }

    // ���õ� ���� ���
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

    // Root Namespace ���� ����:
    // 1) ���� ����� asmdef.rootNamespace
    // 2) ProjectSettings(EditorSettings.projectGenerationRootNamespace)
    // 3) DEFAULT_NAMESPACE
    private static string ResolveRootNamespace(string folder)
    {
        // 1) ����� asmdef ã��
        var asmdefPath = FindNearestAsmdef(folder);
        if (!string.IsNullOrEmpty(asmdefPath))
        {
            var rn = ReadAsmdefRootNamespace(asmdefPath);
            if (IsValidNamespace(rn)) return rn;
        }

        // 2) Project Settings ��
#if UNITY_6000_0_OR_NEWER
        var projectNs = EditorSettings.projectGenerationRootNamespace;
#else
        var projectNs = EditorSettings.projectGenerationRootNamespace; // 2022�� ���� ������Ƽ
#endif
        if (IsValidNamespace(projectNs)) return projectNs;

        // 3) �⺻��
        return DEFAULT_NAMESPACE;
    }

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

    [Serializable]
    private class AsmdefJson { public string rootNamespace; }

    private static string ReadAsmdefRootNamespace(string asmdefPath)
    {
        try
        {
            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefJson>(json);
            return data?.rootNamespace ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    // C# ���ӽ����̽� ��ȿ�� �˻�(���� ����)
    private static bool IsValidNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns)) return false;
        // ����/Ư������ ����, �� ���� ���
        var parts = ns.Split('.');
        foreach (var p in parts)
        {
            if (string.IsNullOrWhiteSpace(p)) return false;
            // ù ���ڴ� ����/����
            if (!(char.IsLetter(p[0]) || p[0] == '_')) return false;
            // �������� ����/����/����
            for (int i = 1; i < p.Length; i++)
                if (!(char.IsLetterOrDigit(p[i]) || p[i] == '_')) return false;
        }
        return true;
    }

    // �̸� ������ ����
    private class DoNothingAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string newName = System.IO.Path.GetFileNameWithoutExtension(pathName);
            Debug.Log($"�� ���� �̸�: {newName}");
        }
    }
}
#endif
