using System.Collections.Generic;

namespace Feather.Analysis
{
    public class ClassMeta
    {
        public string Name { get; set; }
        public bool ExtendsJsBehaviour { get; set; }
        public List<Property> Properties { get; set; } = new List<Property>();
    }
}