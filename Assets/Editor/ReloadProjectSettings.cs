using UnityEditor;
using UnityEngine;

public class ReloadProjectSettings
{
    [MenuItem("Tools/Reload Project Settings")]
    public static void ReloadSettings()
    {
        AssetDatabase.Refresh();
        Debug.Log("Project settings reloaded.");
    }
}
