using UnityEngine;

namespace Feather
{
    /// <summary>
    /// Imported JavaScript behaviour asset (Project window shows as "Java Script", like MonoScript for C#).
    /// </summary>
    public class JavaScript : ScriptableObject
    {
        [SerializeField] [TextArea(8, 32)]
        private string m_ScriptText = string.Empty;

        [SerializeField]
        private string m_ClassName = string.Empty;

        [SerializeField]
        private bool m_ExtendsJsBehaviour;

        [SerializeField]
        private string m_ImportError = string.Empty;

        public string text => m_ScriptText;
        public string ClassName => m_ClassName;
        public bool ExtendsJsBehaviour => m_ExtendsJsBehaviour;
        public string ImportError => m_ImportError;
        public bool HasError => !string.IsNullOrEmpty(m_ImportError);

        public void SetImportData(string scriptText, string className, bool extendsJsBehaviour, string importError = null)
        {
            m_ScriptText = scriptText ?? string.Empty;
            m_ClassName = className ?? string.Empty;
            m_ExtendsJsBehaviour = extendsJsBehaviour;
            m_ImportError = importError ?? string.Empty;
        }
    }
}
