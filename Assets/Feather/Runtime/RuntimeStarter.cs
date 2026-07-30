using UnityEngine;

namespace Feather
{
    public static class RuntimeStarter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            if (Runtime.Instance != null)
                return;

            var runtimeGameObject = new GameObject("FeatherRuntime");
            Object.DontDestroyOnLoad(runtimeGameObject);
            runtimeGameObject.AddComponent<Runtime>();
        }
    }
}
