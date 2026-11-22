// Place this file in Assets/Editor/ (create the Editor folder if it doesn't exist).
// Linux workaround: Unity's Directory Monitoring is Windows-only, so on Linux the
// editor only refreshes when its window regains focus. Under Hyprland/XWayland that
// focus event is unreliable. This watches the Assets folder for script changes and
// triggers a refresh itself.

using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoRefreshOnSave
{
    private static FileSystemWatcher watcher;
    private static volatile bool changed;
    private static double refreshAt;

    // Wait this long after the last change before refreshing, so a burst of saves
    // (e.g. "Save All" in VS Code) only triggers one recompile.
    private const double DebounceSeconds = 0.5;

    static AutoRefreshOnSave()
    {
        string assetsPath = Application.dataPath;

        watcher = new FileSystemWatcher(assetsPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.*"
        };

        watcher.Changed += OnFileEvent;
        watcher.Created += OnFileEvent;
        watcher.Deleted += OnFileEvent;
        watcher.Renamed += OnFileEvent;
        watcher.EnableRaisingEvents = true;

        EditorApplication.update += OnEditorUpdate;

        // Stop watching before a domain reload so watchers don't pile up.
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }
        };
    }

    // Runs on a background thread: only set a flag here. Unity APIs must be
    // called from the main thread (OnEditorUpdate).
    private static void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (ext == ".cs" || ext == ".asmdef" || ext == ".asmref" || ext == ".shader" || ext == ".hlsl")
        {
            changed = true;
        }
    }

    private static void OnEditorUpdate()
    {
        if (changed)
        {
            changed = false;
            refreshAt = EditorApplication.timeSinceStartup + DebounceSeconds;
        }

        if (refreshAt > 0 && EditorApplication.timeSinceStartup >= refreshAt)
        {
            // Don't refresh mid-compile or during Play mode.
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            refreshAt = 0;
            AssetDatabase.Refresh();
        }
    }
}
