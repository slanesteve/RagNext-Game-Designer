using UnityEditor;
using System.IO;

public static class TemplateBuilder
{
    [MenuItem("Build/Build Windows Template")]
    public static void BuildWindowsTemplate()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
        string outputDir = Path.Combine(projectRoot, "..", "Templates", "Windows");
        
        if (Directory.Exists(outputDir))
        {
            try
            {
                Directory.Delete(outputDir, true);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("Could not delete build output directory: " + ex.Message);
            }
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        buildPlayerOptions.locationPathName = Path.Combine(outputDir, "RagNextPlayer.exe");
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
        }
        else
        {
            UnityEngine.Debug.LogError("Build failed: " + summary.result);
        }
    }
}
