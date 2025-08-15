#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

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

    /// <summary>MonoTemplate 템플릿 파일 경로(.cs.txt).</summary>
    private const string MONO_PARTIAL_TEMPLATE = TEMPLATE_DIR + "/MonoTemplate.Editor.cs.txt";         // 에디터 전용 partial

    /// <summary>ScriptableObject 템플릿 파일 경로(.cs.txt).</summary>
    private const string SO_TEMPLATE = TEMPLATE_DIR + "/ScriptableObjectTemplate.cs.txt";

    /// <summary>rootNamespace가 비어있을 때 사용할 기본 네임스페이스.</summary>
    private const string DEFAULT_NAMESPACE = "DefaultNamespace";

    /// <summary>EditorTestConfig 템플릿 파일 경로(.cs.txt).</summary>
    private const string EDITOR_CONFIG_PATH = "Assets/Editor/EditorTestConfig.cs";

    /// <summary>EditorTestConfig 템플릿 (UTF-8 BOM로 저장 예정)</summary>
    private const string EDITOR_CONFIG_TEMPLATE = @"#if UNITY_EDITOR
namespace #ROOTNAMESPACE#
{
    /// <summary>
    /// 에디터 전용 테스트 코드들의 전역 On/Off 스위치.
    /// </summary>
    public static class EditorTestConfig
    {
        /// <summary>true면 에디터 테스트 코드 실행.</summary>
        public static bool Enabled = true;
    }
}
#endif
";

    // ======================= 메뉴 =======================

    /// <summary>
    /// EditorTestConfig 스크립트를 생성합니다(최초 1회). 
    /// Assets/Editor/EditorTestConfig.cs 로 생성되며, UTF-8 BOM으로 저장됩니다.
    /// </summary>
    [MenuItem("Assets/Create/CustomScript/Create EditorTestConfig", false, 50)]
    public static void CreateEditorTestConfig()
    {
        // 현재 Project 창에서 선택한 폴더 경로
        string targetDir = GetSelectedPathOrAssets();
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        string configPath = Path.Combine(targetDir, "EditorTestConfig.cs").Replace("\\", "/");

        if (File.Exists(configPath))
        {
            EditorUtility.DisplayDialog("Already Exists", "이미 EditorTestConfig.cs가 존재합니다.", "OK");
            return;
        }

        string ns = ResolveRootNamespace(targetDir);
        if (string.IsNullOrWhiteSpace(ns)) ns = DEFAULT_NAMESPACE;

        var content = EDITOR_CONFIG_TEMPLATE.Replace("#ROOTNAMESPACE#", ns);
        WriteAllTextUtf8Bom(configPath, content);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", $"EditorTestConfig.cs 생성 완료!\n{configPath}", "OK");
        Debug.Log($"[Create] {configPath}");
    }

    /// <summary>
    /// MonoBehaviour 템플릿으로 새 스크립트를 생성합니다.
    /// (이름 편집 완료 후, 본 클래스와 Editor partial를 함께 생성)
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
            MONO_TEMPLATE // 참고용 전달(필수는 아님)
        );
    }

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
    // ======================= 콜백: 이름 확정 후 두 파일 생성 =======================

    private class CreateMonoWithPartialAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            // 최종 경로/이름
            string basePath = pathName.Replace("\\", "/");                 // ex) Assets/Scripts/Foo.cs
            string dir = Path.GetDirectoryName(basePath).Replace("\\", "/");
            string className = Path.GetFileNameWithoutExtension(basePath);

            // Editor 폴더 생성
            string editorDir = dir.Replace("\\", "/");

            string editorPath = Path.Combine(editorDir, $"{className}.Editor.cs").Replace("\\", "/");

            // 템플릿 존재 확인
            if (!File.Exists(MONO_TEMPLATE) || !File.Exists(MONO_PARTIAL_TEMPLATE))
            {
                EditorUtility.DisplayDialog("Template Missing",
                    $"템플릿을 찾을 수 없습니다.\n{MONO_TEMPLATE}\n{MONO_PARTIAL_TEMPLATE}",
                    "OK");
                return;
            }

            // 덮어쓰기 방지
            if (File.Exists(basePath) || File.Exists(editorPath))
            {
                EditorUtility.DisplayDialog("Already Exists",
                    $"이미 같은 이름의 파일이 존재합니다.\n{basePath}\n{editorPath}",
                    "OK");
                return;
            }

            // 네임스페이스 결정
            string rootNs = ResolveRootNamespace(dir);

            // 본 클래스 생성
            string baseTxt = File.ReadAllText(MONO_TEMPLATE)
                .Replace("#SCRIPTNAME#", className)
                .Replace("#ROOTNAMESPACE#", rootNs);

            // 에디터 partial 생성
            string editorTxt = File.ReadAllText(MONO_PARTIAL_TEMPLATE)
                .Replace("#SCRIPTNAME#", className)
                .Replace("#ROOTNAMESPACE#", rootNs);

            File.WriteAllText(basePath, baseTxt);
            File.WriteAllText(editorPath, editorTxt);

            AssetDatabase.ImportAsset(basePath);
            AssetDatabase.ImportAsset(editorPath);
            ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(basePath));

            Debug.Log($"[Create] {className}.cs + Editor/{className}.Editor.cs 생성 완료");
        }
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
            Debug.Log($"새 파일 이름: {newName}");
        }
    }

    /// <summary>asmdef JSON 역직렬화를 위한 최소 구조체.</summary>
    [Serializable]
    private class AsmdefJson
    {
        public string rootNamespace;
    }

    /// <summary>
    /// UTF-8 with BOM 저장 헬퍼
    /// </summary>
    private static void WriteAllTextUtf8Bom(string path, string text)
    {
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        using (var sw = new StreamWriter(path, false, utf8Bom))
            sw.Write(text);
    }
}
#endif
