using System;
using System.Linq;
using Esprima;
using Feather.Analysis;
using Jint;
using Jint.Native;
using UnityEngine;

namespace Feather
{
    public class ScriptBehaviour : MonoBehaviour
    {
        [Serializable]
        public class BridgeProperties
        {
            public string name;
            public UnityEngine.Object gameObject;
            public UnityEngine.Component component;
        }

        [SerializeField]
        private BridgeProperties[] properties;
        [SerializeField]
        private TextAsset script;

        private void Awake()
        {
            var parser = new JavaScriptParser(script.text);
            var program = parser.ParseScript();
            if (!Analyzer.IsScriptValid(program))
            {
                Debug.LogError("Your Script must contain at least one class declaration.");
                return;
            }
            var scriptMeta = Analyzer.AnalyzeScript(program);
            if (!Analyzer.HasJSBehaviour(scriptMeta))
            {
                Debug.LogError("Your Script must contain at least one jsBehaviour class.");
                return;
            }

            // Load script class into global
            Runtime.Instance.Engine.Execute(program);
            // Create class instance
            var jsBehaviourInstance = Runtime.Instance.InstantiateClass(scriptMeta.Class);
            // Bind jsBehaviour with its hosting GameObject
            jsBehaviourInstance.Set("gameObject", JsValue.FromObject(Runtime.Instance.Engine, this.gameObject));
            jsBehaviourInstance.Set("transform", JsValue.FromObject(Runtime.Instance.Engine, this.transform));

            // Set CLR object references
            foreach (var property in scriptMeta.Class.Properties.Where(p => !string.IsNullOrEmpty(p.Decorator)))
            {
                var matchProperty = properties.SingleOrDefault(p => p.name == property.Name);
                if (matchProperty == null && matchProperty.gameObject != null)
                {
                    return;
                }

                jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, matchProperty.gameObject != null ? matchProperty.gameObject : matchProperty.component));
            }
            jsBehaviourInstance.Get("onStart").AsFunctionInstance().Call(jsBehaviourInstance);
        }
    }
}
