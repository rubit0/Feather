namespace Feather.Analysis
{
    public enum FieldKind
    {
        Unknown = 0,
        UnityObject,
        Float,
        Int,
        Bool,
        String,
        Vector2,
        Vector3,
        Vector4,
        Color,
        UnityEvent
    }

    public class Property
    {
        public string Decorator { get; set; }
        public string Name { get; set; }
        public bool IsArray { get; set; }
        public FieldKind Kind { get; set; } = FieldKind.Unknown;
        public bool HasDecorator => !string.IsNullOrEmpty(Decorator);

        // Default values inferred from JS initializers (used on first attach)
        public float DefaultFloat { get; set; }
        public int DefaultInt { get; set; }
        public bool DefaultBool { get; set; }
        public string DefaultString { get; set; }
        public float DefaultX { get; set; }
        public float DefaultY { get; set; }
        public float DefaultZ { get; set; }
        public float DefaultW { get; set; }
        public bool HasDefault { get; set; }

        // Inspector metadata from @Range / @Tooltip / @Header / …
        public string Tooltip { get; set; }
        public string Header { get; set; }
        public bool HasSpace { get; set; }
        public float SpacePixels { get; set; } = 8f;
        public bool HasRange { get; set; }
        public float RangeMin { get; set; }
        public float RangeMax { get; set; }
        public bool HasMin { get; set; }
        public float MinValue { get; set; }
        public bool HasMax { get; set; }
        public float MaxValue { get; set; }
        public bool TextArea { get; set; }
        public bool Multiline { get; set; }
        public int MultilineLines { get; set; } = 3;
        public bool Required { get; set; }
        public bool SceneObjectsOnly { get; set; }
        public bool AssetsOnly { get; set; }
        public bool LayerField { get; set; }
        public bool TagField { get; set; }
        public bool HasColorUsage { get; set; }
        public bool ColorUsageHdr { get; set; }
        public bool ColorUsageShowAlpha { get; set; } = true;

        /// <summary>
        /// When Decorator is JavaScriptBehaviour, optional JS class filter
        /// (e.g. <c>other = scriptTest</c> → <c>scriptTest</c>).
        /// </summary>
        public string JsBehaviourClass { get; set; }
    }
}
