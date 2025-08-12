using UnityEditor;

public static class ScriptTemplatesMenu
{
    [MenuItem("Assets/Create/CustomScript/MonoBehaviour", false, 1)]
    public static void CreateCustomMono()
    {
        string t = "Assets/Editor/ScriptTemplates/MonoBehaviourTemplate.cs.txt";
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(t, "NewMonoBehaviour.cs");
    }

    [MenuItem("Assets/Create/CustomScript/ScriptableObject", false, 2)]
    public static void CreateCustomSO()
    {
        string t = "Assets/Editor/ScriptTemplates/ScriptableObjectTemplate.cs.txt";
        ProjectWindowUtil.CreateScriptAssetFromTemplateFile(t, "NewScriptableObject.cs");
    }
}