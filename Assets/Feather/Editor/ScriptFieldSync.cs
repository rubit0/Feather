using System.Collections.Generic;
using System.Linq;
using Feather.Analysis;
using UnityEngine;
using UnityEngine.Events;

namespace Feather.Editor
{
    public static class ScriptFieldSync
    {
        public static bool Sync(JavaScriptBehaviour behaviour, JavaScript scriptAsset, bool applyDefaults = true)
        {
            if (behaviour == null || scriptAsset == null)
                return false;

            if (!Analyzer.TryAnalyze(scriptAsset.text, out var meta, out var error))
            {
                Debug.LogWarning($"[Feather] Cannot sync fields for {scriptAsset.name}: {error}");
                return false;
            }

            if (!Analyzer.HasJSBehaviour(meta))
            {
                Debug.LogWarning($"[Feather] {scriptAsset.name} must extend jsBehaviour");
                return false;
            }

            var existing = behaviour.properties?.ToList() ?? new List<JavaScriptBehaviour.BridgeProperties>();
            var updated = new List<JavaScriptBehaviour.BridgeProperties>();

            foreach (var prop in meta.Class.Properties)
            {
                var bridge = existing.FirstOrDefault(ep => ep.name == prop.Name);
                if (bridge == null)
                {
                    bridge = new JavaScriptBehaviour.BridgeProperties
                    {
                        name = prop.Name,
                        kind = JavaScriptBehaviour.KindFromAnalysis(prop),
                        isList = prop.IsArray,
                        unityEvent = new UnityEvent(),
                        gameObjectList = prop.IsArray ? new List<Object>() : null,
                        componentList = prop.IsArray ? new List<Component>() : null,
                        unityEventList = prop.IsArray ? new List<UnityEvent>() : null
                    };
                    if (applyDefaults)
                        JavaScriptBehaviour.ApplyDefaults(bridge, prop);
                }
                else
                {
                    bridge.kind = JavaScriptBehaviour.KindFromAnalysis(prop);
                    bridge.isList = prop.IsArray;
                    if (prop.IsArray)
                    {
                        bridge.gameObjectList ??= new List<Object>();
                        bridge.componentList ??= new List<Component>();
                        bridge.unityEventList ??= new List<UnityEvent>();
                    }
                    if (bridge.kind == JavaScriptBehaviour.BridgeKind.UnityEvent && bridge.unityEvent == null)
                        bridge.unityEvent = new UnityEvent();
                }

                updated.Add(bridge);
            }

            behaviour.properties = updated.ToArray();
            return true;
        }
    }
}
