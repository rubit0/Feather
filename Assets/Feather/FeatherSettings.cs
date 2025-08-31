using UnityEngine;

namespace Feather
{
    [System.Serializable]
    public static class FeatherSettings
    {
        // Set to true to enable verbose logging for debugging
        public static bool VerboseLogging = false;
        
        // Set to true to log script loading
        public static bool LogScriptLoading = false;
        
        // Set to true to log component addition
        public static bool LogComponentAddition = false;
        
        public static void Log(string message)
        {
            if (VerboseLogging)
            {
                Debug.Log($"[Feather] {message}");
            }
        }
        
        public static void LogScriptLoad(string message)
        {
            if (LogScriptLoading)
            {
                Debug.Log($"[Feather] {message}");
            }
        }
        
        public static void LogComponentAdd(string message)
        {
            if (LogComponentAddition)
            {
                Debug.Log($"[Feather] {message}");
            }
        }
    }
}
