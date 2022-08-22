using UnityEngine;

namespace Feather.Misc
{
    public class RuntimeStarter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            var runtimeGameObject = new GameObject("FeatherRuntime");
            runtimeGameObject.AddComponent<Runtime>();
        }
    }
}