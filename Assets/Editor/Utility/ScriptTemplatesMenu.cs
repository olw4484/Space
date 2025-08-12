#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

/// <summary>
/// 유니티 에디터에서 커스텀 스크립트 템플릿을 기반으로
/// MonoBehaviour, ScriptableObject, Partial 클래스 파일을 안전하게 생성하는 유틸 클래스.
/// </summary>
/// <remarks>
/// - 템플릿 파일(.cs.txt)은 반드시 <c>Assets/Editor/ScriptTemplates</c> 폴더에 위치해야 합니다.
/// - <c>#SCRIPTNAME#</c> 및 <c>#ROOTNAMESPACE#</c> 토큰을 치환합니다.
/// - 네임스페이스 결정 우선순위:
///   1) 가장 가까운 asmdef의 rootNamespace
///   2) Project Settings → Root Namespace
///   3) DEFAULT_NAMESPACE 상수
/// </remarks>
public static class SafeScriptCreator
{
    /// <summary>프로젝트 로컬 템플릿 폴더 경로.</summary>
    private const string TEMPLATE_DIR = "Assets/Editor/ScriptTemplates";

    /// <summary>MonoBehaviour 템플릿 파일 경로(.cs.txt).</summary>
    private const string MONO_TEMPLATE = TEMPLATE_DIR + "/MonoBehaviourTemplate.cs.txt";

    /// <summary>ScriptableObject 템플릿 파일 경로(.cs.txt).</summary>
    private const string SO_TEMPLATE = TEMPLATE_DIR + "/ScriptableObjectTemplate.cs.txt";

    /// <summary>rootNamespace가 비어있을 때 사용할 기본 네임스페이스.</summary>
    private const string DEFAULT_NAMESPACE = "DefaultNamespace";

    // ======================= 메뉴 =======================

    /// <summary>
    /// MonoBehaviour 템플릿으로 새 스크립트를 생성합니다.
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/MonoBehaviour (safe)", false, 1)]
    public static void CreateMono()
        => CreateFromTemplate(MONO_TEMPLATE, "NewMonoBehaviour.cs");

    /// <summary>
    /// ScriptableObject 템플릿으로 새 스크립트를 생성합니다.
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/ScriptableObject (safe)", false, 2)]
    public static void CreateSO()
        => CreateFromTemplate(SO_TEMPLATE, "NewScriptableObject.cs");

    // ======================= 본체 =======================

    /// <summary>
    /// 지정한 템플릿 파일을 읽어 클래스명/네임스페이스를 치환한 후,
    /// 지정한 경로에 새 C# 스크립트를 생성합니다.
    /// </summary>
    /// <param name="templatePath">템플릿 파일 경로(.cs.txt)</param>
    /// <param name="defaultName">기본 생성 파일명(확장자 포함)</param>
    private static void CreateFromTemplate(string templatePath, string defaultName)
    {
        if (!File.Exists(templatePath))
        {
            EditorUtility.DisplayDialog(
                "Template Missing",
                $"템플릿을 찾을 수 없습니다.\n{templatePath}\n\n경로/파일명을 확인하세요(.cs.txt 필요).",
                "OK"
            );
            return;
        }

        // 생성 위치/이름 결정
        string targetDir = GetSelectedPathOrAssets();
        string outPath = Path.Combine(targetDir, defaultName).Replace("\\", "/");
        string className = Path.GetFileNameWithoutExtension(outPath);

        // 템플릿 로드 및 토큰 치환
        string txt = File.ReadAllText(templatePath);
        string rootNs = ResolveRootNamespace(targetDir);

        txt = txt.Replace("#SCRIPTNAME#", className)
                 .Replace("#ROOTNAMESPACE#", rootNs);

        File.WriteAllText(outPath, txt);
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outPath);
    }

    /// <summary>
    /// 현재 선택된 경로를 반환합니다.
    /// - 폴더를 선택한 경우 해당 폴더 경로를 반환
    /// - 파일을 선택한 경우 해당 파일의 부모 폴더 경로를 반환
    /// - 선택이 없는 경우 "Assets"를 반환
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
    /// 지정된 폴더를 기준으로 네임스페이스를 결정합니다.
    /// </summary>
    /// <param name="folder">검색 기준 폴더 경로</param>
    /// <returns>네임스페이스 문자열</returns>
    private static string ResolveRootNamespace(string folder)
    {
        // 1) 가까운 asmdef
        var asmdefPath = FindNearestAsmdef(folder);
        if (!string.IsNullOrEmpty(asmdefPath))
        {
            var rn = ReadAsmdefRootNamespace(asmdefPath);
            if (IsValidNamespace(rn)) return rn;
        }

        // 2) Project Settings
        var projectNs = EditorSettings.projectGenerationRootNamespace;
        if (IsValidNamespace(projectNs)) return projectNs;

        // 3) 기본값
        return DEFAULT_NAMESPACE;
    }

    /// <summary>
    /// 주어진 폴더에서 상위로 탐색하며 가장 가까운 .asmdef 파일 경로를 반환합니다.
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
    /// asmdef JSON에서 rootNamespace 값을 읽어옵니다.
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
    /// 문자열이 유효한 C# 네임스페이스인지 검사합니다.
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
    /// MonoBehaviour + partial Editor 스크립트를 동시에 생성합니다.
    /// </summary>
    private static void CreateMonoWithPartial()
    {
        string baseTemplate = TEMPLATE_DIR + "/MonoTemplate.cs.txt";
        string editorTemplate = TEMPLATE_DIR + "/MonoTemplate.Editor.cs.txt";

        string targetDir = GetSelectedPathOrAssets();
        string basePath = Path.Combine(targetDir, "NewScript.cs").Replace("\\", "/");
        string editorPath = basePath.Replace(".cs", ".Editor.cs");

        string className = Path.GetFileNameWithoutExtension(basePath);
        string rootNs = ResolveRootNamespace(targetDir);

        File.WriteAllText(basePath,
            File.ReadAllText(baseTemplate)
                .Replace("#SCRIPTNAME#", className)
                .Replace("#ROOTNAMESPACE#", rootNs));

        File.WriteAllText(editorPath,
            File.ReadAllText(editorTemplate)
                .Replace("#SCRIPTNAME#", className)
                .Replace("#ROOTNAMESPACE#", rootNs));

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 이름 편집 후 추가 작업을 처리하는 더미 클래스.
    /// 현재는 단순히 partial 생성 메서드를 호출합니다.
    /// </summary>
    private class DoNothingAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string newName = Path.GetFileNameWithoutExtension(pathName);
            CreateMonoWithPartial();
            Debug.Log($"새 파일 이름: {newName}");
        }
    }

    /// <summary>asmdef JSON 역직렬화를 위한 최소 구조체.</summary>
    [Serializable]
    private class AsmdefJson
    {
        public string rootNamespace;
    }
}
#endif
