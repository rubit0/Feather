using System;
using System.Collections.Generic;
using System.Linq;
using Feather.Analysis;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using UnityEngine;
using UnityEngine.Events;

namespace Feather
{
    public class ScriptBehaviour : MonoBehaviour
    {
        [Serializable]
        public class BridgeProperties
        {
            public string name;
            public bool isList;
            public UnityEngine.Object gameObject;
            public Component component;
            public UnityEvent unityEvent;
            public System.Collections.Generic.List<UnityEngine.Object> gameObjectList = new System.Collections.Generic.List<UnityEngine.Object>();
            public System.Collections.Generic.List<Component> componentList = new System.Collections.Generic.List<Component>();
            public System.Collections.Generic.List<UnityEvent> unityEventList = new System.Collections.Generic.List<UnityEvent>();
        }

        [SerializeField]
        public BridgeProperties[] properties;
        [SerializeField]
        public TextAsset script;

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
                Debug.LogError($"JavaScript class '{scriptName}' not found in loaded scripts. " +
                              $"Make sure the script file '{script.name}' contains a valid class extending jsBehaviour. " +
                              $"Available scripts: {string.Join(", ", Runtime.Instance.LoadedScripts.Keys)}");
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
                if (matchProperty == null)
                {
                    continue;
                }

                if (property.IsArray)
                {
                    // Inject list properties (convert to arrays for JavaScript)
                    if (property.Decorator == "GameObject")
                    {
                        // Use GameObject list for @List(GameObject)
                        var listToInject = matchProperty.gameObjectList != null ? 
                            matchProperty.gameObjectList.ToArray() : 
                            new UnityEngine.Object[0];
                        _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, listToInject));
                    }
                    else if (property.Decorator == "UnityEvent")
                    {
                        // Use UnityEvent list for @List(UnityEvent) - handle separately since UnityEvent doesn't inherit from UnityEngine.Object
                        var unityEventArray = matchProperty.unityEventList != null ? 
                            matchProperty.unityEventList.ToArray() : 
                            new UnityEvent[0];
                        _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, unityEventArray));
                    }
                    else
                    {
                        // Use Component list for @List(Button), @List(Light), etc.
                        var listToInject = matchProperty.componentList != null ? 
                            matchProperty.componentList.ToArray() : 
                            new Component[0];
                        _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, listToInject));
                    }
                }
                else
                {
                    // Inject single properties
                    if (property.Decorator == "UnityEvent")
                    {
                        // Handle UnityEvent separately (it doesn't inherit from UnityEngine.Object)
                        if (matchProperty.unityEvent != null)
                        {
                            _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, matchProperty.unityEvent));
                        }
                    }
                    else
                    {
                        // Handle regular Unity objects
                        var objectToInject = matchProperty.gameObject != null ? 
                            matchProperty.gameObject : 
                            matchProperty.component;
                        
                        if (objectToInject != null)
                        {
                            _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, objectToInject));
                        }
                    }
                }
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
        
        public void ReloadScript()
        {
            if (script == null) return;
            
            // Clear old callbacks and instance
            _gameObjectLifeCycleCallbacks.Clear();
            _jsBehaviourInstance = null;
            
            // Reinitialize with the reloaded script
            var scriptName = script.name.Split('.')[0];
            if (!Runtime.Instance.LoadedScripts.ContainsKey(scriptName))
            {
                Debug.LogError($"Cannot reload - JavaScript class '{scriptName}' not found in loaded scripts.");
                return;
            }
            
            // Get updated meta reference
            _scriptMeta = Runtime.Instance.LoadedScripts[scriptName];

            // Create new class instance
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
                if (matchProperty == null)
                {
                    continue;
                }

                if (property.IsArray)
                {
                    // Inject list properties (convert to arrays for JavaScript)
                    if (property.Decorator == "GameObject")
                    {
                        // Use GameObject list for @List(GameObject)
                        var listToInject = matchProperty.gameObjectList != null ? 
                            matchProperty.gameObjectList.ToArray() : 
                            new UnityEngine.Object[0];
                        _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, listToInject));
                    }
                    else if (property.Decorator == "UnityEvent")
                    {
                        // Use UnityEvent list for @List(UnityEvent) - handle separately since UnityEvent doesn't inherit from UnityEngine.Object
                        var unityEventArray = matchProperty.unityEventList != null ? 
                            matchProperty.unityEventList.ToArray() : 
                            new UnityEvent[0];
                        _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, unityEventArray));
                    }
                    else
                    {
                        // Use Component list for @List(Button), @List(Light), etc.
                        var listToInject = matchProperty.componentList != null ? 
                            matchProperty.componentList.ToArray() : 
                            new Component[0];
                        _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, listToInject));
                    }
                }
                else
                {
                    // Inject single properties
                    if (property.Decorator == "UnityEvent")
                    {
                        // Handle UnityEvent separately (it doesn't inherit from UnityEngine.Object)
                        if (matchProperty.unityEvent != null)
                        {
                            _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, matchProperty.unityEvent));
                        }
                    }
                    else
                    {
                        // Handle regular Unity objects
                        var objectToInject = matchProperty.gameObject != null ? 
                            matchProperty.gameObject : 
                            matchProperty.component;
                        
                        if (objectToInject != null)
                        {
                            _jsBehaviourInstance.Set(property.Name, JsValue.FromObject(Runtime.Instance.Engine, objectToInject));
                        }
                    }
                }
            }

            // Call Awake on the reloaded instance if it exists
            if (_gameObjectLifeCycleCallbacks.ContainsKey("Awake"))
            {
                _gameObjectLifeCycleCallbacks["Awake"].Call(_jsBehaviourInstance);
            }
        }
    }
}
