using System;
using System.Collections.Generic;
using System.Linq;
using Feather.Analysis;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
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
            public Component component;
        }

        [SerializeField]
        private BridgeProperties[] properties;
        [SerializeField]
        private TextAsset script;

        private ScriptMeta _scriptMeta;
        private ObjectInstance _jsBehaviourInstance;
        private readonly Dictionary<string, FunctionInstance> _gameObjectLifeCycleCallbacks 
            = new Dictionary<string, FunctionInstance>();
        private static readonly string[] LifecycleMethodNames =
        {
            "Awake", "Start", "OnEnable", "OnDisable", "Update", "LateUpdate", "FixedUpdate", "OnDestroy"
        };

        private void Awake()
        {
            var scriptName = script.name.Split('.')[0];
            if (!Runtime.Instance.LoadedScripts.ContainsKey(scriptName))
            {
                Debug.LogError($"Set script {script.name}is not valid.");
                return;
            }
            
            // Get meta reference
            _scriptMeta = Runtime.Instance.LoadedScripts[scriptName];

            // Create class instance
            _jsBehaviourInstance = Runtime.Instance.InstantiateClass(_scriptMeta.Class);
            // Bind jsBehaviour with its hosting GameObject
            _jsBehaviourInstance.Set("gameObject", JsValue.FromObject(Runtime.Instance.Engine, this.gameObject));
            _jsBehaviourInstance.Set("transform", JsValue.FromObject(Runtime.Instance.Engine, this.transform));

            // Check for unity lifecycle methods
            foreach (var methodName in _scriptMeta.Class.Methods)
            {
                if (!LifecycleMethodNames.Contains(methodName))
                {
                    continue;
                }
                
                _gameObjectLifeCycleCallbacks.Add(methodName, _jsBehaviourInstance.Get(methodName).AsFunctionInstance());
            }
            
            // Set CLR object references
            foreach (var property in _scriptMeta.Class.Properties.Where(p => !string.IsNullOrEmpty(p.Decorator)))
            {
                var matchProperty = properties.SingleOrDefault(p => p.name == property.Name);
                if (matchProperty == null && matchProperty.gameObject != null)
                {
                    return;
                }

                _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, matchProperty.gameObject != null ? matchProperty.gameObject : matchProperty.component));
            }

            if (_gameObjectLifeCycleCallbacks.ContainsKey("Awake"))
            {
                _gameObjectLifeCycleCallbacks["Awake"].Call(_jsBehaviourInstance);
            }
        }

        private void Start()
        {
            if (_gameObjectLifeCycleCallbacks.ContainsKey("Start"))
            {
                _gameObjectLifeCycleCallbacks["Start"].Call(_jsBehaviourInstance);
            }
        }

        private void OnEnable()
        {
            if (_gameObjectLifeCycleCallbacks.ContainsKey("OnEnable"))
            {
                _gameObjectLifeCycleCallbacks["OnEnable"].Call(_jsBehaviourInstance);
            }
        }
        
        private void OnDisable()
        {
            if (_gameObjectLifeCycleCallbacks.ContainsKey("OnDisable"))
            {
                _gameObjectLifeCycleCallbacks["OnDisable"].Call(_jsBehaviourInstance);
            }
        }
        
        private void Update()
        {
            if (_gameObjectLifeCycleCallbacks.ContainsKey("Update"))
            {
                _gameObjectLifeCycleCallbacks["Update"].Call(_jsBehaviourInstance);
            }
        }
        
        private void LateUpdate()
        {
            if (_gameObjectLifeCycleCallbacks.ContainsKey("LateUpdate"))
            {
                _gameObjectLifeCycleCallbacks["LateUpdate"].Call(_jsBehaviourInstance);
            }
        }
        
        private void FixedUpdate()
        {
            if (_gameObjectLifeCycleCallbacks.ContainsKey("FixedUpdate"))
            {
                _gameObjectLifeCycleCallbacks["FixedUpdate"].Call(_jsBehaviourInstance);
            }
        }
        
        private void OnDestroy()
        {
            if (_gameObjectLifeCycleCallbacks.ContainsKey("OnDestroy"))
            {
                _gameObjectLifeCycleCallbacks["OnDestroy"].Call(_jsBehaviourInstance);
            }
        }
    }
}
