using System;
using System.Collections.Generic;

namespace MonsterTools.Core
{
    public class ToolRouter
    {
        private readonly Dictionary<string, IToolWorker> _registeredWorkers = new(StringComparer.OrdinalIgnoreCase);

        public void RegisterWorker(IToolWorker worker)
        {
            if (worker == null) throw new ArgumentNullException(nameof(worker));
            _registeredWorkers[worker.Name] = worker;
        }

        public IToolWorker GetWorker(string toolName)
        {
            if (_registeredWorkers.TryGetValue(toolName, out var worker))
            {
                return worker;
            }
            throw new KeyNotFoundException($"No deterministic system worker registered for tool: '{toolName}'");
        }

        public bool HasWorker(string toolName) => _registeredWorkers.ContainsKey(toolName);
    }
}
