#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoulsMaze.EditorTools
{
    /// <summary>
    /// 1. 自動在 Assets/Resources 建立兩個材質範本，確保打包後 URP Lit 與發光效果不會被刪掉。
    /// 2. 上方選單「SoulsMaze → 打包網頁版 (WebGL)」一鍵輸出到專案的 Builds/WebGL 資料夾。
    /// </summary>
    [InitializeOnLoad]
    public static class SoulsMazeBuild
    {
        const string ResDir = "Assets/Resources";

        static SoulsMazeBuild()
        {
            EditorApplication.delayCall += EnsureMaterials;
        }

        public static void EnsureMaterials()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return;
            if (!AssetDatabase.IsValidFolder(ResDir)) AssetDatabase.CreateFolder("Assets", "Resources");
            Make("SoulsMazeLit", sh, false);
            Make("SoulsMazeLitEmissive", sh, true);
        }

        static void Make(string name, Shader sh, bool emissive)
        {
            string path = ResDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var m = new Material(sh);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("SoulsMaze/打包網頁版 (WebGL)")]
        public static void BuildWebGL()
        {
            EnsureMaterials();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                // 先把專案平台切到 Web（會重新匯入資源，需要一點時間），切完再按一次選單打包
                bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                Debug.Log(ok ? "[SoulsMaze] 已切換平台到 Web，等進度條跑完後，再按一次「SoulsMaze → 打包網頁版」。"
                             : "[SoulsMaze] 切換到 Web 平台失敗，請確認 Unity Hub 已安裝 Web Build Support。");
                return;
            }
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            PlayerSettings.productName = "SoulsMaze";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // 放到任何網站（例如 itch.io）都能正常解壓
            PlayerSettings.stripEngineCode = true; // 需要保留的元件寫在 Assets/link.xml

            string scene = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scene)) scene = "Assets/Scenes/SampleScene.unity";

            string outDir = System.IO.Path.GetFullPath("Builds/WebGL");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.CleanBuildCache // 清掉舊的打包快取，從頭打包
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            WriteReport(report, outDir);
            Debug.Log($"[SoulsMaze] WebGL 打包結果：{report.summary.result}，大小 {report.summary.totalSize / (1024f * 1024f):0.0} MB，位置：{System.IO.Path.GetFullPath(outDir)}");
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(System.IO.Path.Combine(outDir, "index.html"));
        }

        static void WriteReport(BuildReport report, string outDir)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("result: " + report.summary.result);
            sb.AppendLine("platform: " + report.summary.platform);
            sb.AppendLine("outputPath: " + report.summary.outputPath);
            sb.AppendLine("totalSize: " + report.summary.totalSize);
            sb.AppendLine("errors: " + report.summary.totalErrors + "  warnings: " + report.summary.totalWarnings);
            sb.AppendLine("activeTarget: " + EditorUserBuildSettings.activeBuildTarget);
            sb.AppendLine("outDir exists: " + System.IO.Directory.Exists(outDir));
            try { sb.AppendLine("files listed in report: " + report.GetFiles().Length); } catch (System.Exception e) { sb.AppendLine("GetFiles failed: " + e.Message); }
            foreach (var step in report.steps)
            {
                sb.AppendLine($"== {step.name} ({step.duration.TotalSeconds:0.0}s)");
                foreach (var m in step.messages)
                    if (m.type != LogType.Log) sb.AppendLine($"   [{m.type}] {m.content}");
            }
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath("SoulsMaze_build_report.txt"), sb.ToString());
        }
    }
}
#endif
