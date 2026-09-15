using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Checks the checked-in engine pin and release identifiers before every build.</summary>
public sealed class ProjectVersionValidation : IPreprocessBuildWithReport
{
    public int callbackOrder => -2000;

    public void OnPreprocessBuild(BuildReport report)
    {
        string expected = null;
        foreach (string line in File.ReadLines("ProjectSettings/ProjectVersion.txt"))
            if (line.StartsWith("m_EditorVersion: ", StringComparison.Ordinal))
                expected = line.Substring("m_EditorVersion: ".Length).Trim();
        if (expected != Application.unityVersion)
            throw new BuildFailedException($"Use Unity {expected}; running {Application.unityVersion}.");

        string[] version = PlayerSettings.bundleVersion.Split('.');
        if (version.Length != 3 || !ValidPart(version[0]) || !ValidPart(version[1]) || !ValidPart(version[2]))
            throw new BuildFailedException("Game version must use MAJOR.MINOR.PATCH, for example 0.9.0.");
        if (report.summary.platform == BuildTarget.Android && PlayerSettings.Android.bundleVersionCode < 1)
            throw new BuildFailedException("Android versionCode must be positive and increase for each published build.");
    }

    static bool ValidPart(string value) => int.TryParse(value, out int number) && number >= 0 && number.ToString() == value;
}
