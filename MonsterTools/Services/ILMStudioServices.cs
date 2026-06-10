using System.Collections.Generic;

namespace MonsterTools.Core
{
    /// <summary>
    /// Normalises raw unformatted payload values sent from lower-compute local models.
    /// </summary>
    public interface IToolArgumentNormalizer
    {
        Dictionary<string, object> Normalize(Dictionary<string, object>? sourceArguments);
    }
}
