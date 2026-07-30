using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Feather.Editor
{
    /// <summary>
    /// Convenience: after C# compile/domain reload, refresh <c>Project.d.ts</c> in the background
    /// when project Component types change. Avoids AssetDatabase work and skips unchanged output.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectDefinitionAutoUpdater
    {
        private static int _updateQueued;
        private static int _writeInFlight;
        private static int _retryFrames;
        private static CancellationTokenSource _cts;
        private static readonly ConcurrentQueue<string> _mainThreadLogs = new ConcurrentQueue<string>();
        private static readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        static ProjectDefinitionAutoUpdater()
        {
            AssemblyReloadEvents.afterAssemblyReload += ScheduleUpdate;
            CompilationPipeline.compilationFinished += _ => ScheduleUpdate();
            EditorApplication.update += PumpMainThreadWork;
        }

        private static void ScheduleUpdate()
        {
            if (Interlocked.CompareExchange(ref _updateQueued, 1, 0) != 0)
                return;

            _retryFrames = 0;
            EditorApplication.delayCall += ProcessScheduledUpdate;
        }

        private static void ProcessScheduledUpdate()
        {
            Interlocked.Exchange(ref _updateQueued, 0);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Wait until compile finished so Assembly-CSharp types are available.
            if (EditorApplication.isCompiling)
            {
                if (_retryFrames++ < 120)
                {
                    if (Interlocked.CompareExchange(ref _updateQueued, 1, 0) == 0)
                        EditorApplication.delayCall += ProcessScheduledUpdate;
                }
                return;
            }

            BeginBackgroundUpdate();
        }

        private static void BeginBackgroundUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (Interlocked.CompareExchange(ref _writeInFlight, 1, 0) != 0)
                return;

            string fingerprint;
            string text;
            try
            {
                TypeScriptDefinitionGenerator.MigrateLegacyStampFiles();
                fingerprint = TypeScriptDefinitionGenerator.ComputeProjectDefinitionsFingerprint();
                if (string.Equals(
                        TypeScriptDefinitionGenerator.GetStoredProjectDefinitionsFingerprint(),
                        fingerprint,
                        StringComparison.Ordinal))
                {
                    Interlocked.Exchange(ref _writeInFlight, 0);
                    return;
                }

                text = TypeScriptDefinitionGenerator.BuildProjectDefinitionsText(fingerprint);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _writeInFlight, 0);
                EnqueueLog($"[Feather] Project.d.ts auto-update skipped (reflect): {ex.Message}", warning: true);
                return;
            }

            var defsPath = TypeScriptDefinitionGenerator.ProjectDefinitionsPath;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(() =>
            {
                try
                {
                    var wrote = WriteDefsIfChanged(defsPath, text, token);
                    EnqueueAction(() =>
                    {
                        TypeScriptDefinitionGenerator.StoreProjectDefinitionsFingerprint(fingerprint);
                        AssetDatabase.SaveAssets();
                    });
                    if (wrote)
                        EnqueueLog("[Feather] Project.d.ts updated in background (project types changed).", warning: false);
                }
                catch (OperationCanceledException)
                {
                    // superseded by a newer compile
                }
                catch (Exception ex)
                {
                    EnqueueLog($"[Feather] Project.d.ts auto-update failed: {ex.Message}", warning: true);
                }
                finally
                {
                    Interlocked.Exchange(ref _writeInFlight, 0);
                }
            }, token);
        }

        private static void EnqueueLog(string message, bool warning)
        {
            if (!warning && !FeatherSettings.VerboseLogging)
                return;
            if (warning && !FeatherSettings.VerboseLogging)
                return; // keep convenience feature quiet unless verbose
            _mainThreadLogs.Enqueue(message);
        }

        private static void EnqueueAction(Action action)
        {
            if (action != null)
                _mainThreadActions.Enqueue(action);
        }

        private static void PumpMainThreadWork()
        {
            while (_mainThreadActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Debug.LogWarning($"[Feather] {ex.Message}"); }
            }

            while (_mainThreadLogs.TryDequeue(out var message))
            {
                if (message.Contains("failed") || message.Contains("skipped"))
                    Debug.LogWarning(message);
                else
                    Debug.Log(message);
            }
        }

        /// <returns>True when the definitions file was written.</returns>
        private static bool WriteDefsIfChanged(string defsPath, string text, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (File.Exists(defsPath))
            {
                var existingText = File.ReadAllText(defsPath);
                if (BodiesEqual(existingText, text))
                    return false;
            }

            token.ThrowIfCancellationRequested();
            File.WriteAllText(defsPath, text);
            return true;
        }

        private static bool BodiesEqual(string a, string b) =>
            string.Equals(StripHeader(a), StripHeader(b), StringComparison.Ordinal);

        private static string StripHeader(string text)
        {
            using var reader = new StringReader(text ?? "");
            var sb = new System.Text.StringBuilder(text?.Length ?? 0);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("// Generated at:", StringComparison.Ordinal) ||
                    line.StartsWith("// Fingerprint:", StringComparison.Ordinal))
                    continue;
                sb.AppendLine(line);
            }
            return sb.ToString();
        }
    }
}
