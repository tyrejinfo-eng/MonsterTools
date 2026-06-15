using System;
using System.IO;
using System.Collections.Concurrent;
using System.Timers;

namespace MonsterTools.Workers
{
    public class WorkspaceListener
    {
        private FileSystemWatcher _watcher;
        private readonly string _workspacePath;
        private readonly ConcurrentDictionary<string, DateTime> _changedFilesDebounce;
        private readonly Timer _debounceTimer;
        private const int DebounceDelayMs = 500; // Stabilizes rapid multi-write disk operations

        public WorkspaceListener(string workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            {
                throw new ArgumentException($"Invalid or non-existent target workspace path: {workspacePath}");
            }

            _workspacePath = workspacePath;
            _changedFilesDebounce = new ConcurrentDictionary<string, DateTime>();
            
            // Setting up file system debounce processing timer
            _debounceTimer = new Timer(DebounceDelayMs);
            _debounceTimer.Elapsed += OnDebounceTimerElapsed;
            _debounceTimer.AutoReset = true;
        }

        public void StartListening()
        {
            _watcher = new FileSystemWatcher(_workspacePath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName 
                             | NotifyFilters.DirectoryName 
                             | NotifyFilters.LastWrite 
                             | NotifyFilters.Size,
                Filter = "*.*" // Observes all development source asset formats
            };

            // Bind change execution event handlers
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileRenameEvent;

            _watcher.EnableRaisingEvents = true;
            _debounceTimer.Start();

            // Direct pipeline update out to your custom VS Code Sidebar layout view
            TelemetryStreamer.PushEngineUpdate(
                "FileSystem", 
                "Observing workspace changes...", 
                0.0, 0, 0, 
                $"[SYS] Real-time file system observer attached to: {_workspacePath}", 
                "sys"
            );
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            // Filter out common dependency bloating directories to maintain context cleanliness
            if (e.FullPath.Contains("\\bin\\") || 
                e.FullPath.Contains("\\obj\\") || 
                e.FullPath.Contains("\\.git\\") || 
                e.FullPath.Contains("\\node_modules\\"))
            {
                return;
            }

            _changedFilesDebounce[e.FullPath] = DateTime.UtcNow;
            
            string visualAction = e.ChangeType.ToString().ToUpper();
            TelemetryStreamer.PushEngineUpdate(
                "FileSystem", 
                "Tracking modification event...", 
                0.0, 0, 0, 
                $"[DISK] File {visualAction}: {Path.GetFileName(e.FullPath)}", 
                "worker"
            );
        }

        private void OnFileRenameEvent(object sender, RenamedEventArgs e)
        {
            if (e.FullPath.Contains("\\bin\\") || e.FullPath.Contains("\\obj\\") || e.FullPath.Contains("\\.git\\")) return;

            TelemetryStreamer.PushEngineUpdate(
                "FileSystem", 
                "Tracking rename operation...", 
                0.0, 0, 0, 
                $"[DISK] Renamed: {e.OldName} -> {e.Name}", 
                "worker"
            );

            // Signal your main core logic context frame that an indexing shift has occurred
            Core.AgentLoop.NotifyWorkspaceMutation();
        }

        private void OnDebounceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.UtcNow;
            foreach (var kp in _changedFilesDebounce)
            {
                if ((now - kp.Value).TotalMilliseconds >= DebounceDelayMs)
                {
                    if (_changedFilesDebounce.TryRemove(kp.Key, out _))
                    {
                        // Notify your AgentLoop state machine to safely ingest the updated file
                        Core.AgentLoop.HandleFileMutationUpdate(kp.Key);
                    }
                }
            }
        }

        public void StopListening()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
            _debounceTimer.Stop();
            _debounceTimer.Dispose();
        }
    }
}
