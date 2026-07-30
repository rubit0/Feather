using System.Collections.Generic;

namespace Feather.Analysis
{
    public class ScriptMeta
    {
        public List<string> Imports { get; set; } = new List<string>();
        public ClassMeta Class { get; set; }
    }
}