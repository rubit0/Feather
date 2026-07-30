using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    /// <summary>
    /// Ensures the JS project (defs / jsconfig / link.xml) exists after Feather is installed
    /// or when the Unity API stamp goes stale (e.g. Unity upgrade).
    /// </summary>
    [InitializeOnLoad]
    public static class TypeScriptDefinitionProcessor
    {
        private const string SessionKey = "Feather.JsProjectAutoSetupAttempted";

        static TypeScriptDefinitionProcessor()
        {
            EditorApplication.delayCall += TryEnsureJsProject;
        }

        private static void TryEnsureJsProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryEnsureJsProject;
                return;
            }

            if (TypeScriptDefinitionGenerator.JsProjectIsCurrent())
                return;

            // One auto attempt per editor session — avoids loops if generation fails.
            if (SessionState.GetBool(SessionKey, false))
                return;
            SessionState.SetBool(SessionKey, true);

            Debug.Log("[Feather] JS project missing or outdated — generating automatically…");
            TypeScriptDefinitionGenerator.GenerateOrUpdateJsProject(quiet: true);
        }
    }
}
