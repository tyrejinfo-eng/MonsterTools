using System.Collections.Generic;

namespace MonsterTools.Core
{
    public class ToolArgumentNormalizer : IToolArgumentNormalizer
    {
        public Dictionary<string, object> Normalize(Dictionary<string, object>? sourceArguments)
        {
            if (sourceArguments == null)
            {
                return new Dictionary<string, object>();
            }

            var cleanMap = new Dictionary<string, object>();
            foreach (var kvp in sourceArguments)
            {
                if (kvp.Value == null) continue;
                
                // Strip unnecessary enclosing quotes often added by small models
                if (kvp.Value is string stringValue)
                {
                    var cleanValue = stringValue.Trim('\"', '\'');
                    cleanMap[kvp.Key] = cleanValue;
                }
                else
                {
                    cleanMap[kvp.Key] = kvp.Value;
                }
            }
            return cleanMap;
        }
    }
}
