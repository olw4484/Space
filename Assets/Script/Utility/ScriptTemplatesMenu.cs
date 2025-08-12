#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

public static class SafeScriptCreator
{
    // 프로젝트 로컬 템플릿 위치
    private const string TEMPLATE_DIR = "Assets/Editor/ScriptTemplates";
    private const string MONO_TEMPLATE = TEMPLATE_DIR + "/MonoBehaviourTemplate.cs.txt";
    private const string SO_TEMPLATE = TEMPLATE_DIR + "/ScriptableObjectTemplate.cs.txt";

    // Root Namespace 비었을 때 사용할 기본값
    private const string DEFAULT_NAMESPACE = "DefaultNamespace";

    // =============== 메뉴 ===============
    [MenuItem("Assets/Create/CustomScript/MonoBehaviour (safe)", false, 1)]
    public static void CreateMono()
        => CreateFromTemplate(MONO_TEMPLATE, "NewMonoBehaviour.cs");

    [MenuItem("Assets/Create/CustomScript/ScriptableObject (safe)", false, 2)]
    public static void CreateSO()
        => CreateFromTemplate(SO_TEMPLATE, "NewScriptableObject.cs");

    // =============== 본체 ===============
    private static void CreateFromTemplate(string templatePath, string defaultName)
    {
        if (!File.Exists(templatePath))
        {
            EditorUtility.DisplayDialog("Template Missing",
                $"템플릿을 찾을 수 없습니다.\n{templatePath}\n\n" +
                "경로/파일명을 확인하세요(.cs.txt 필요).", "OK");
            return;
        }

        // 생성 위치/이름
        string targetDir = GetSelectedPathOrAssets();
        string outPath = Path.Combine(targetDir, defaultName).Replace("\\", "/");
        string className = Path.GetFileNameWithoutExtension(outPath);

        // 템플릿 로드 & 치환
        string txt = File.ReadAllText(templatePath);
        string rootNs = ResolveRootNamespace(targetDir);

        txt = txt.Replace("#SCRIPTNAME#", className)
                 .Replace("#ROOTNAMESPACE#", rootNs);

        File.WriteAllText(outPath, txt);
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outPath);
        // 바로 이름 바꾸기 모드로 진입하려면:
        // ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoNothingAction>(), outPath, null, null);
    }

    // 선택된 폴더 경로
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

    // Root Namespace 결정 로직:
    // 1) 가장 가까운 asmdef.rootNamespace
    // 2) ProjectSettings(EditorSettings.projectGenerationRootNamespace)
    // 3) DEFAULT_NAMESPACE
    private static string ResolveRootNamespace(string folder)
    {
        // 1) 가까운 asmdef 찾기
        var asmdefPath = FindNearestAsmdef(folder);
        if (!string.IsNullOrEmpty(asmdefPath))
        {
            var rn = ReadAsmdefRootNamespace(asmdefPath);
            if (IsValidNamespace(rn)) return rn;
        }

        // 2) Project Settings 값
#if UNITY_6000_0_OR_NEWER
        var projectNs = EditorSettings.projectGenerationRootNamespace;
#else
        var projectNs = EditorSettings.projectGenerationRootNamespace; // 2022도 동일 프로퍼티
#endif
        if (IsValidNamespace(projectNs)) return projectNs;

        // 3) 기본값
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
            // Assets 루트 밖으로 나가면 중단
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

    // C# 네임스페이스 유효성 검사(간단 판정)
    private static bool IsValidNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns)) return false;
        // 공백/특수문자 제거, 점 구분 허용
        var parts = ns.Split('.');
        foreach (var p in parts)
        {
            if (string.IsNullOrWhiteSpace(p)) return false;
            // 첫 문자는 문자/밑줄
            if (!(char.IsLetter(p[0]) || p[0] == '_')) return false;
            // 나머지는 문자/숫자/밑줄
            for (int i = 1; i < p.Length; i++)
                if (!(char.IsLetterOrDigit(p[i]) || p[i] == '_')) return false;
        }
        return true;
    }

    // 이름 편집용 더미
    private class DoNothingAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string newName = System.IO.Path.GetFileNameWithoutExtension(pathName);
            Debug.Log($"새 파일 이름: {newName}");
        }
    }
}
#endif
