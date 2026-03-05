using UnityEditor;
using UnityEngine;
using System.IO;

public static class DenEmoProjectPrefs
{
    // Use absolute project path (Assets folder path) as project key to scope prefs per project
    public static string ProjectKey
    {
        get
        {
            string path = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            return path;
        }
    }

    static string Scoped(string key) => $"DenEmo|{ProjectKey}|{key}";

    public static bool HasKey(string key) => EditorPrefs.HasKey(Scoped(key));
    public static void DeleteKey(string key) => EditorPrefs.DeleteKey(Scoped(key));

    public static void SetString(string key, string value) => EditorPrefs.SetString(Scoped(key), value);
    public static string GetString(string key, string defaultValue = "") => EditorPrefs.GetString(Scoped(key), defaultValue);

    public static void SetInt(string key, int value) => EditorPrefs.SetInt(Scoped(key), value);
    public static int GetInt(string key, int defaultValue = 0) => EditorPrefs.GetInt(Scoped(key), defaultValue);

    public static void SetBool(string key, bool value) => EditorPrefs.SetBool(Scoped(key), value);
    public static bool GetBool(string key, bool defaultValue = false) => EditorPrefs.GetBool(Scoped(key), defaultValue);
}
