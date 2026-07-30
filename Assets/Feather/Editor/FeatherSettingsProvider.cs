using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    public static class FeatherSettingsProvider
    {
        public const string ProjectSettingsPath = "Project/Feather";
        public const string PreferencesPath = "Preferences/Feather";

        [SettingsProvider]
        public static SettingsProvider CreateProjectSettings()
        {
            return CreateProvider(ProjectSettingsPath, SettingsScope.Project);
        }

        [SettingsProvider]
        public static SettingsProvider CreatePreferences()
        {
            // Same project asset — Preferences is where people look for “which editor opens files”.
            return CreateProvider(PreferencesPath, SettingsScope.User);
        }

        private static SettingsProvider CreateProvider(string path, SettingsScope scope)
        {
            FeatherSettings settings = null;
            SerializedObject serialized = null;
            DiscoverableApiPackage[] discovered = null;
            var packageScroll = Vector2.zero;

            var provider = new SettingsProvider(path, scope)
            {
                label = "Feather",
                // guiHandler is wrapped in Unity’s scroll view — works when the panel is short.
                guiHandler = _ =>
                {
                    if (settings == null)
                    {
                        settings = FeatherSettings.GetOrCreateSettings();
                        serialized = settings != null ? new SerializedObject(settings) : null;
                        discovered = null;
                    }

                    if (settings == null || serialized == null)
                    {
                        EditorGUILayout.HelpBox("FeatherSettings asset missing.", MessageType.Error);
                        return;
                    }

                    if (discovered == null && !EditorApplication.isCompiling)
                        discovered = ApiPackageDiscovery.Discover();

                    serialized.Update();
                    var packagesChanged = false;

                    EditorGUILayout.HelpBox(
                        TypeScriptDefinitionGenerator.JsProjectIsCurrent()
                            ? "JS project looks up to date."
                            : "JS project is missing or outdated — generate/update recommended.",
                        TypeScriptDefinitionGenerator.JsProjectIsCurrent()
                            ? MessageType.Info
                            : MessageType.Warning);

                    using (new EditorGUI.DisabledScope(
                               EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button("Generate / Update JS Project", GUILayout.Height(28)))
                            TypeScriptDefinitionGenerator.GenerateOrUpdateJsProject(quiet: false);
                    }

                    EditorGUILayout.Space(12);
                    EditorGUILayout.LabelField("JavaScript Code Editor", EditorStyles.boldLabel);
                    DrawProp(serialized, "javascriptEditor", "Open .js files with",
                        "Auto prefers Cursor, then VS Code. Unity default uses Edit → Preferences → External Tools.");

                    var editorProp = serialized.FindProperty("javascriptEditor");
                    if (editorProp != null &&
                        (JavaScriptEditorPreference)editorProp.intValue == JavaScriptEditorPreference.Custom)
                    {
                        DrawProp(serialized, "customJavaScriptEditorPath", "Custom editor path",
                            "Path to a CLI (cursor/code) or .app");
                    }

                    EditorGUILayout.Space(12);
                    EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
                    DrawProp(serialized, "verboseLogging");
                    DrawProp(serialized, "logScriptLoading");
                    DrawProp(serialized, "logComponentAddition");

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                    DrawProp(serialized, "allowSystemReflection", "Allow System.Reflection",
                        "When enabled, JS can use System.Reflection / GetType on CLR objects. Off by default. Does not change which Unity assemblies are exposed.");
                    DrawProp(serialized, "playerScriptsResourcesPath", "Scripts path",
                        "Resources-relative folder used for player script collection.");

                    EditorGUILayout.Space(12);
                    EditorGUILayout.LabelField("API Packages", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "Opt into installed UPM packages for JS AllowClr and IntelliSense. " +
                        "UnityEngine.* types appear under Unity.*; other namespaces become globals with dots as underscores " +
                        "(e.g. Unity.AI.Navigation → Unity_AI_Navigation).",
                        MessageType.None);

                    if (EditorApplication.isCompiling)
                    {
                        EditorGUILayout.HelpBox("Discovering packages after compile…", MessageType.Info);
                    }
                    else if (discovered == null || discovered.Length == 0)
                    {
                        EditorGUILayout.LabelField("No optional packages with player assemblies found.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        var enabled = new HashSet<string>(
                            settings.enabledApiPackageIds ?? Array.Empty<string>(),
                            StringComparer.OrdinalIgnoreCase);

                        packageScroll = EditorGUILayout.BeginScrollView(packageScroll, GUILayout.MaxHeight(220));
                        foreach (var pkg in discovered)
                        {
                            var on = enabled.Contains(pkg.Id);
                            var next = EditorGUILayout.ToggleLeft(
                                new GUIContent(
                                    pkg.Label,
                                    $"{pkg.Id}@{pkg.Version}\nAssemblies: {string.Join(", ", pkg.AssemblyNames)}"),
                                on);
                            if (next == on) continue;

                            packagesChanged = true;
                            if (next) enabled.Add(pkg.Id);
                            else enabled.Remove(pkg.Id);
                        }
                        EditorGUILayout.EndScrollView();

                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Refresh list", GUILayout.Width(100)))
                            discovered = ApiPackageDiscovery.Discover();
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(
                            $"{enabled.Count} selected / {discovered.Length} available",
                            EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();

                        if (packagesChanged)
                        {
                            settings.enabledApiPackageIds = enabled.OrderBy(id => id).ToArray();
                            settings.extraClrAssemblies =
                                ApiPackageDiscovery.ResolveAssembliesForPackages(settings.enabledApiPackageIds);
                            EditorUtility.SetDirty(settings);
                            serialized.Update();
                        }
                    }

                    var otherChanged = serialized.ApplyModifiedProperties();
                    if (otherChanged)
                    {
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssets();
                    }

                    if (packagesChanged)
                    {
                        AssetDatabase.SaveAssets();
                        if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
                            TypeScriptDefinitionGenerator.GenerateOrUpdateJsProject(quiet: true);
                    }
                },
                keywords = new[]
                {
                    "Feather", "JavaScript", "Editor", "Cursor", "VS Code", "Rider",
                    "IntelliSense", "jsconfig", "External Tools", "Open", "Generate", "d.ts",
                    "Package", "UPM", "AllowClr", "Cinemachine", "Input System"
                }
            };
            return provider;
        }

        private static void DrawProp(
            SerializedObject serialized,
            string name,
            string label = null,
            string tooltip = null)
        {
            var prop = serialized.FindProperty(name);
            if (prop == null) return;

            if (label != null)
                EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip ?? prop.tooltip));
            else
                EditorGUILayout.PropertyField(prop);
        }
    }
}
