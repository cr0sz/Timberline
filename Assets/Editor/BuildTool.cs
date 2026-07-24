using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-button Android build for itch.io / sideloading.
///
/// Debug-signed on purpose: Unity falls back to its own debug keystore when no custom
/// one is configured, which is all an itch.io download needs. A Play Store upload needs
/// a real keystore and its passwords, which do not belong in a repo or in a script —
/// set those up in Publishing Settings by hand.
/// </summary>
public static class BuildTool
{
    const string Scene = "Assets/Scenes/Map.unity";

    [MenuItem("Tools/Survival/Build Android APK")]
    public static void BuildApk()
    {
        var root = Directory.GetParent(Application.dataPath).FullName;
        var outDir = Path.Combine(root, "Builds");
        Directory.CreateDirectory(outDir);
        var apk = Path.Combine(outDir, $"Survival-{PlayerSettings.bundleVersion}.apk");
        var log = Path.Combine(outDir, "build.log");

        // Identity. Left at Unity's DefaultCompany placeholder the package would ship as
        // com.DefaultCompany.Survival, which collides with every other unconfigured
        // project on the device.
        PlayerSettings.companyName = "cr0sz";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.cr0sz.survival");

        // The game is portrait-only and drives its own framerate in MobileBootstrap.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        EditorUserBuildSettings.buildAppBundle = false;   // APK, not AAB — AAB is a Play-only format
        EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        File.WriteAllText(log, $"build started {DateTime.Now:s}\n");
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(opts);
        }
        catch (Exception e)
        {
            File.AppendAllText(log, $"EXCEPTION {e}\n");
            Debug.LogError($"[BUILD] threw: {e.Message}");
            return;
        }

        var s = report.summary;
        var line = $"result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                   $"size={s.totalSize / (1024 * 1024)}MB time={s.totalTime} out={apk}";
        File.AppendAllText(log, line + "\n");

        if (s.result == BuildResult.Succeeded) Debug.Log($"[BUILD] OK — {line}");
        else Debug.LogError($"[BUILD] FAILED — {line}");
    }
}
