using System.Collections.Generic;
using Feather.Analysis;
using Jint;
using Jint.Native.Object;
using UnityEngine;

namespace Feather
{
    public class Runtime : MonoBehaviour
    {
        public static Runtime Instance  { get; private set; }
        public Engine Engine { get; private set; }
        public Dictionary<string, ScriptMeta> LoadedScripts { get; set; } 
            = new Dictionary<string, ScriptMeta>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            Engine = new Engine(cfg =>
            {
                cfg.Debugger.Enabled = true;
                cfg.Interop.AllowGetType = true;
                cfg.Interop.AllowSystemReflection = true;
                cfg.AllowClr(
                    typeof(GameObject).Assembly, 
                    typeof(Rigidbody).Assembly, 
                    typeof(AudioListener).Assembly,
                    typeof(Input).Assembly, 
                    typeof(Canvas).Assembly
                    );
            });
            Engine.Execute("var Unity = importNamespace('UnityEngine');");
            // Define base interface class
            Engine.Execute(@"class jsBehaviour {
                                @GameObject
                                gameObject;
                                @Transform
                                transform
                            }");
            var allScripts = Resources.LoadAll<TextAsset>("Scripts");
            foreach (var scriptText in allScripts)
            {
                var body = scriptText.text;
                var script = Analyzer.ParseScript(body);
                if (Analyzer.IsScriptValid(script))
                {
                    Engine.Execute(script);
                    var scriptMeta = Analyzer.AnalyzeScript(script);
                    if (Analyzer.HasJSBehaviour(scriptMeta))
                    {
                        LoadedScripts.Add(scriptMeta.Class.Name, scriptMeta);
                    }
                }
            }
        }

        public ObjectInstance InstantiateClass(ClassMeta classMeta)
        {
            return Engine.Evaluate($"return new {classMeta.Name}();").AsObject();
        }
    }
}
