/// <summary>
/// 유니티 에디터에서 커스텀 스크립트 템플릿을 기반으로
/// MonoBehaviour 또는 ScriptableObject 스크립트를 안전하게 생성하는 유틸 클래스.
/// </summary>
/// <remarks>
/// - 템플릿 파일(.cs.txt)은 반드시 <c>Assets/Editor/ScriptTemplates</c> 폴더에 위치해야 합니다.
/// - <c>#SCRIPTNAME#</c> 및 <c>#ROOTNAMESPACE#</c> 토큰을 치환합니다.
/// - 네임스페이스 결정 우선순위: 가장 가까운 asmdef의 <c>rootNamespace</c> → Project Settings → <c>DEFAULT_NAMESPACE</c>.
/// - <c>ProjectWindowUtil.StartNameEditingIfProjectWindowExists</c> 사용 시 이름 편집 콜백을 위해 내부 더미 클래스를 제공합니다.
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
    /// 프로젝트 로컬 템플릿 폴더 경로.
    /// </summary>
    private const string TEMPLATE_DIR = "Assets/Editor/ScriptTemplates";

    /// <summary>
    /// MonoBehaviour 템플릿 파일 경로(.cs.txt).
    /// </summary>
    private const string MONO_TEMPLATE = TEMPLATE_DIR + "/MonoBehaviourTemplate.cs.txt";

    /// <summary>
    /// ScriptableObject 템플릿 파일 경로(.cs.txt).
    /// </summary>
    private const string SO_TEMPLATE = TEMPLATE_DIR + "/ScriptableObjectTemplate.cs.txt";

    /// <summary>
    /// rootNamespace가 비어있을 때 사용할 기본 네임스페이스.
    /// </summary>
    private const string DEFAULT_NAMESPACE = "DefaultNamespace";

    // =============== 메뉴 ===============

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

    // =============== 본체 ===============

    /// <summary>
    /// 템플릿을 읽어 클래스명/네임스페이스 토큰을 치환한 뒤 새 스크립트를 생성합니다.
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

        // 이름 즉시 편집 모드 진입이 필요할 때:
        // ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
        //     0, ScriptableObject.CreateInstance<DoNothingAction>(), outPath, null, null
        // );
    }

    /// <summary>
    /// 현재 선택된 에셋이 폴더라면 그 경로를, 파일이라면 부모 폴더 경로를 반환합니다.
    /// 선택이 없으면 <c>Assets</c>를 반환합니다.
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
    /// 네임스페이스를 결정합니다. 가까운 asmdef의 <c>rootNamespace</c>가 있으면 우선 사용하고,
    /// 없으면 Project Settings의 Root Namespace를 사용하며, 둘 다 없거나 무효면 기본값을 반환합니다.
    /// </summary>
    /// <param name="folder">생성 대상 폴더 경로</param>
    private static string ResolveRootNamespace(string folder)
    {
        // 1) 가까운 asmdef
        var asmdefPath = FindNearestAsmdef(folder);
        if (!string.IsNullOrEmpty(asmdefPath))
        {
            var rn = ReadAsmdefRootNamespace(asmdefPath);
            if (IsValidNamespace(rn)) return rn;
        }

        // 2) Project Settings 값
        var projectNs = EditorSettings.projectGenerationRootNamespace; // 2022/6000 공통
        if (IsValidNamespace(projectNs)) return projectNs;

        // 3) 기본값
        return DEFAULT_NAMESPACE;
    }

    /// <summary>
    /// 주어진 폴더에서 상위로 거슬러 올라가며 가장 가까운 <c>.asmdef</c> 파일을 찾습니다.
    /// </summary>
    /// <param name="folder">검색 시작 폴더</param>
    /// <returns>asmdef 파일 경로 또는 null</returns>
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

    /// <summary>
    /// asmdef JSON에서 <c>rootNamespace</c> 값을 읽어옵니다.
    /// 파싱 실패 시 빈 문자열을 반환합니다.
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
    /// C# 네임스페이스로서 유효한지 간단 규칙으로 검사합니다.
    /// 문자/밑줄 시작, 이후 문자/숫자/밑줄 허용, <c>.</c> 구분 허용.
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
    /// 이름 편집 후 추가 작업을 위해 필요한 더미 콜백 클래스입니다.
    /// 현재는 기능이 없으며, 필요 시 <see cref="Action"/> 내부에 후처리를 구현하세요.
    /// </summary>
    private class DoNothingAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        /// <summary>
        /// 이름 편집 완료 시 호출됩니다. 현재는 로그만 출력합니다.
        /// </summary>
        /// <param name="instanceId">인스턴스 ID</param>
        /// <param name="pathName">최종 경로(파일명 포함)</param>
        /// <param name="resourceFile">리소스 파일 경로(템플릿)</param>
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string newName = Path.GetFileNameWithoutExtension(pathName);
            Debug.Log($"새 파일 이름: {newName}");
        }
    }

    /// <summary>
    /// asmdef JSON 역직렬화를 위한 최소 구조체.
    /// </summary>
    [Serializable]
    private class AsmdefJson
    {
        /// <summary>asmdef의 rootNamespace 값.</summary>
        public string rootNamespace;
    }
}
#endif
