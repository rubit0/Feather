using System;
using System.Diagnostics;
using System.IO;
using Unity.CodeEditor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Feather.Editor
{
    /// <summary>
    /// Opens Feather .js files for editing with JS IntelliSense in mind.
    /// Prefer Cursor/VS Code with the Unity project root as workspace (loads jsconfig.json).
    /// Opening only inside Rider's Unity/.sln context usually gives little/no JS IntelliSense.
    /// </summary>
    public static class FeatherOpenAssetHandler
    {
        [OnOpenAsset(-100)]
        public static bool OnOpenAsset(EntityId entityId, int line) => OnOpenAsset(entityId, line, 0);

        [OnOpenAsset(-100)]
        public static bool OnOpenAsset(EntityId entityId, int line, int column)
        {
            var assetPath = AssetDatabase.GetAssetPath(entityId);
            return TryOpenJavaScript(assetPath, line, column);
        }

        public static bool TryOpenJavaScript(UnityEngine.Object asset, int line = 1, int column = 0)
        {
            if (asset == null) return false;
            return TryOpenJavaScript(AssetDatabase.GetAssetPath(asset), line, column);
        }

        public static bool TryOpenJavaScript(string assetPath, int line = 1, int column = 0)
        {
            if (string.IsNullOrEmpty(assetPath) || !IsFeatherScriptPath(assetPath))
                return false;

            // Consume open so Unity never uses OS .js → browser association.
            var absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[Feather] Cannot open script — file not found: {assetPath}");
                return true;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var lineNumber = line > 0 ? line : 1;
            var columnNumber = Math.Max(1, column);

            if (TryOpenInJsAwareEditor(projectRoot, absolutePath, lineNumber, columnNumber))
                return true;

            // Explicit fallback: Unity External Script Editor (C# context — weaker JS IntelliSense)
            if (TryOpenWithUnityExternalEditor(absolutePath, lineNumber, columnNumber))
                return true;

            try
            {
                if (InternalEditorUtility.OpenFileAtLineExternal(assetPath, lineNumber, columnNumber))
                    return true;
                if (InternalEditorUtility.OpenFileAtLineExternal(assetPath, lineNumber))
                    return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Feather] OpenFileAtLineExternal failed: {ex.Message}");
            }

            Debug.LogError(
                "[Feather] Could not open JavaScript in a JS-aware editor.\n" +
                "Install Cursor/VS Code, or set Project Settings → Feather → JavaScript Editor.\n" +
                $"File: {absolutePath}");
            return true;
        }

        private static bool TryOpenInJsAwareEditor(string projectRoot, string absoluteFilePath, int line, int column)
        {
            var settings = FeatherSettings.Instance ?? FeatherSettings.GetOrCreateSettings();
            var preference = settings != null
                ? settings.javascriptEditor
                : JavaScriptEditorPreference.AutoPreferJsIde;

            switch (preference)
            {
                case JavaScriptEditorPreference.UnityExternalScriptEditor:
                    return false; // handled by caller fallback

                case JavaScriptEditorPreference.Cursor:
                    return TryLaunchVsCodeFamily(FindCursorCli(), projectRoot, absoluteFilePath, line, column, "Cursor");

                case JavaScriptEditorPreference.VSCode:
                    return TryLaunchVsCodeFamily(FindVsCodeCli(), projectRoot, absoluteFilePath, line, column, "VS Code");

                case JavaScriptEditorPreference.Custom:
                    return TryLaunchCustom(settings.customJavaScriptEditorPath, projectRoot, absoluteFilePath, line, column);

                case JavaScriptEditorPreference.AutoPreferJsIde:
                default:
                    if (TryLaunchVsCodeFamily(FindCursorCli(), projectRoot, absoluteFilePath, line, column, "Cursor"))
                        return true;
                    if (TryLaunchVsCodeFamily(FindVsCodeCli(), projectRoot, absoluteFilePath, line, column, "VS Code"))
                        return true;
                    return false;
            }
        }

        /// <summary>
        /// Open project root as workspace + jump to file:line so jsconfig.json / Feather.d.ts apply.
        /// Example: cursor "/path/to/Feather" -g "/path/to/file.js:12:1"
        /// </summary>
        private static bool TryLaunchVsCodeFamily(string cli, string projectRoot, string absoluteFilePath, int line, int column, string label)
        {
            if (string.IsNullOrEmpty(cli) || !File.Exists(cli))
                return false;

            var args = $"\"{projectRoot}\" -g \"{absoluteFilePath}:{line}:{column}\"";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cli,
                    Arguments = args,
                    UseShellExecute = false
                });
                FeatherSettings.Log($"Opened JavaScript in {label} (workspace IntelliSense): {absoluteFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Feather] Failed to launch {label}: {ex.Message}");
                return false;
            }
        }

        private static bool TryLaunchCustom(string editorPath, string projectRoot, string absoluteFilePath, int line, int column)
        {
            if (string.IsNullOrWhiteSpace(editorPath))
                return false;

            editorPath = editorPath.Trim();
            var lower = editorPath.Replace('\\', '/').ToLowerInvariant();

            // If custom points at Cursor/VS Code CLI or .app, use workspace mode
            var cli = ResolveCliFromPath(editorPath);
            if (!string.IsNullOrEmpty(cli) && (lower.Contains("cursor") || lower.Contains("code")))
                return TryLaunchVsCodeFamily(cli, projectRoot, absoluteFilePath, line, column, "Custom JS editor");

#if UNITY_EDITOR_OSX
            if (editorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(editorPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "/usr/bin/open",
                        Arguments = $"-a \"{editorPath}\" \"{projectRoot}\" --args -g \"{absoluteFilePath}:{line}:{column}\"",
                        UseShellExecute = false
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Feather] Custom .app open failed: {ex.Message}");
                }
            }
#endif

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = editorPath,
                    Arguments = $"\"{absoluteFilePath}\"",
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Feather] Custom editor launch failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryOpenWithUnityExternalEditor(string absolutePath, int line, int column)
        {
            var editorPath = CodeEditor.CurrentEditorPath;
            if (string.IsNullOrEmpty(editorPath))
                return false;

            // Still try CodeEditor.OpenProject first (works for some editors)
            try
            {
                if (CodeEditor.CurrentEditor != null &&
                    CodeEditor.CurrentEditor.OpenProject(absolutePath, line, column))
                    return true;
            }
            catch
            {
                // ignore
            }

            var args = BuildExternalEditorArguments(editorPath, absolutePath, line, column);
            try
            {
                if (CodeEditor.OSOpenFile(editorPath, args))
                    return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Feather] External Script Editor OSOpenFile failed: {ex.Message}");
            }

#if UNITY_EDITOR_OSX
            return TryMacOpenExternalApp(editorPath, absolutePath, line, column);
#else
            return false;
#endif
        }

#if UNITY_EDITOR_OSX
        private static bool TryMacOpenExternalApp(string editorPath, string absolutePath, int line, int column)
        {
            try
            {
                var app = editorPath.TrimEnd('/');
                if (!app.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    return false;

                var lower = app.ToLowerInvariant();
                if (lower.Contains("rider"))
                {
                    var riderBin = Path.Combine(app, "Contents/MacOS/rider");
                    if (File.Exists(riderBin))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = riderBin,
                            Arguments = $"--line {line} \"{absolutePath}\"",
                            UseShellExecute = false
                        });
                        return true;
                    }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "/usr/bin/open",
                    Arguments = $"-a \"{app}\" \"{absolutePath}\"",
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Feather] macOS external editor launch failed: {ex.Message}");
                return false;
            }
        }
#endif

        private static string BuildExternalEditorArguments(string editorPath, string absolutePath, int line, int column)
        {
            var name = editorPath.Replace('\\', '/').ToLowerInvariant();
            if (name.Contains("code") || name.Contains("cursor") || name.Contains("vscodium"))
                return $"-g \"{absolutePath}:{line}:{Math.Max(1, column)}\"";
            if (name.Contains("rider") || name.Contains("idea") || name.Contains("jetbrains"))
                return $"--line {line} \"{absolutePath}\"";
            if (name.Contains("devenv"))
                return $"/edit \"{absolutePath}\"";
            return $"\"{absolutePath}\"";
        }

        private static string FindCursorCli()
        {
            return FirstExisting(
                "/usr/local/bin/cursor",
                "/opt/homebrew/bin/cursor",
                "/Applications/Cursor.app/Contents/Resources/app/bin/cursor",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ?? "",
                    "Programs/cursor/resources/app/bin/cursor.cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ?? "",
                    "Programs/cursor/Cursor.exe")
            );
        }

        private static string FindVsCodeCli()
        {
            return FirstExisting(
                "/usr/local/bin/code",
                "/opt/homebrew/bin/code",
                "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ?? "",
                    "Programs/Microsoft VS Code/bin/code.cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) ?? "",
                    "Microsoft VS Code/Code.exe")
            );
        }

        private static string ResolveCliFromPath(string path)
        {
            if (File.Exists(path)) return path;
            if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
            {
                var lower = path.ToLowerInvariant();
                if (lower.Contains("cursor"))
                {
                    var cli = Path.Combine(path, "Contents/Resources/app/bin/cursor");
                    if (File.Exists(cli)) return cli;
                }
                if (lower.Contains("visual studio code") || lower.Contains("code.app"))
                {
                    var cli = Path.Combine(path, "Contents/Resources/app/bin/code");
                    if (File.Exists(cli)) return cli;
                }
            }
            return null;
        }

        private static string FirstExisting(params string[] paths)
        {
            foreach (var p in paths)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    return p;
            }
            return null;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath) && File.Exists(assetPath))
                return Path.GetFullPath(assetPath);
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('\\', '/')));
        }

        private static bool IsFeatherScriptPath(string assetPath)
        {
            return assetPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                   || assetPath.EndsWith(".jsu", StringComparison.OrdinalIgnoreCase)
                   || assetPath.EndsWith(".jsfeather", StringComparison.OrdinalIgnoreCase);
        }
    }
}
