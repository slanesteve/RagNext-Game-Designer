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

            // Delete debug and backup folders that shouldn't be packaged/shipped (handles arbitrary naming across platforms)
            try
            {
                foreach (var dir in Directory.GetDirectories(outputDir))
                {
                    string dirName = Path.GetFileName(dir);
                    if (dirName.Contains("BurstDebugInformation_DoNotShip") || dirName.Contains("BackUpThisFolder"))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("Failed to clean up IL2CPP debug directories: " + ex.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogError("Build failed: " + summary.result);
        }
    }
}
