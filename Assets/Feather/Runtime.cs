using System;
using Feather.Analysis;
using Jint;
using Jint.Native.Object;
using UnityEngine;
using UnityEngine.UI;

namespace Feather
{
    public class Runtime : MonoBehaviour
    {
        public static Runtime Instance  { get; private set; }
        public Engine Engine { get; private set; }
        
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
                cfg.AllowClr(typeof(GameObject).Assembly, typeof(Text).Assembly);
            });
            Engine.SetValue("float", new Func<double, float>(num => (float)num));
            Engine.Execute("var unity = importNamespace('UnityEngine');");
            // Define base interface class
            Engine.Execute(@"class jsBehaviour {
                                @GameObject
                                gameObject;
                                @Transform
                                transform
                            }");
        }

        public ObjectInstance InstantiateClass(ClassMeta classMeta)
        {
            return Engine.Evaluate($"return new {classMeta.Name}();").AsObject();
        }
    }
}
