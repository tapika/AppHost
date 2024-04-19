using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Timer = System.Threading.Timer;

/// <summary>
/// Main class for initializing C# script hosting services. C# script service user needs to register first static callback functions
/// In order for script hosting service to be application neutral.
/// </summary>
public class ScriptHost
{
    public delegate void delegateConsolePrintLine(string line);
    /// <summary>
    /// Application provided callback function for printing errors like compile, load, etc...
    /// </summary>
    static public delegateConsolePrintLine ConsolePrintLine;

    public delegate void delegateConsoleClear();
    /// <summary>
    /// Application provided callback to clear output window
    /// </summary>
    static public delegateConsoleClear ConsoleClear;


    public delegate void delegateOnUIThread(System.Action action);
    /// <summary>
    /// Application provided callback function for starting action within UI thread.
    /// </summary>
    static public delegateOnUIThread OnUIThread;

    static List<String> dirsToMonitor = new List<string>();
    /// <summary>
    /// The timer used to compress the notification events.
    /// </summary>
    private static Timer m_timer;
    
    /// <summary>
    /// C# scripts to monitor for changes. file to monitor - file to recompile
    /// </summary>
    static Dictionary<String, string> scriptsToMonitor = new Dictionary<string, string>();

    /// <summary>
    /// Map from child C# script to it's master script
    /// </summary>
    static Dictionary<string, List<string>> dependencyScriptToMonitor = new Dictionary<string, List<string>>();

    static List<FileSystemWatcher> fswatchers = new List<FileSystemWatcher>();


    public static void ObserveScript(string masterScript, string childScript = null, 
        bool compileMasterScript = true)
    {
        if (childScript != null)
        {
            childScript = Path.GetFullPath(childScript);        // Normalize without ".."
        }

        if (childScript == null)
        {
            if (scriptsToMonitor.ContainsKey(masterScript))
                return;
        }
        else
        {
            if (!dependencyScriptToMonitor.ContainsKey(childScript))
            {
                dependencyScriptToMonitor.Add(childScript, new List<String>());
            }

            if (dependencyScriptToMonitor[childScript].Contains(masterScript))
            {
                return;
            }
        }

        foreach (string newDir in new[] { childScript, masterScript }.Where(x => x != null).Select(x => Path.GetDirectoryName(x)))
        {
            if (!dirsToMonitor.Contains(newDir))
            {
                FileSystemWatcher watcher = new FileSystemWatcher();
                watcher.Path = newDir;
                watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                watcher.Filter = "*.*";     // We are observing *.cs and *.xaml, but multiple filter extensions are not supported (https://stackoverflow.com/a/6965193/2338477)
                watcher.Changed += FileChanged;
                watcher.Created += FileChanged;
                watcher.Renamed += FileChanged;
                // Begin watching.
                watcher.EnableRaisingEvents = true;
                fswatchers.Add(watcher);
                dirsToMonitor.Add(newDir);
            }
        }

        m_timer = new Timer(new TimerCallback(OnReloadMasterScripts), null, Timeout.Infinite, Timeout.Infinite);

        if (childScript == null)
        {
            scriptsToMonitor.Add(masterScript, masterScript);
            FileReload(masterScript);
        }
        else
        {
            if (compileMasterScript)
            {
                dependencyScriptToMonitor[childScript].Add(masterScript);
            }
            else
            {
                if (scriptsToMonitor.ContainsKey(masterScript))
                { 
                    scriptsToMonitor.Remove(masterScript);
                }
                scriptsToMonitor.Add(masterScript, childScript);
            }
        }

    }


    /// <summary>
    /// Triggered multiple times when file is changed. 
    /// </summary>
    private static void FileChanged(object sender, FileSystemEventArgs e)
    {
        FileReload(e.FullPath);
    }

    /// <summary>
    /// List of main scripts to reload.
    /// </summary>
    static List<string> masterPathsToReload = new List<string>();


    static private void OnReloadMasterScripts(object state)
    {
        foreach (var masterPath in masterPathsToReload)
        {
            OnUIThread(() =>
                {
                    UiFileReload(masterPath);
                }
            );
        }

        masterPathsToReload.Clear();
    }

    /// <summary>
    /// The default amount of time to wait after receiving notification
    /// before reloading the script file.
    /// </summary>
    public static int ScriptUpdatedReloadDelay { get; set; } = 100;

    static void FileReload(String file)
    {
        List<string> paths2process = new List<string>();

        if (scriptsToMonitor.ContainsKey(file))
        {
            paths2process.Add(scriptsToMonitor[file]);
        }

        if (dependencyScriptToMonitor.ContainsKey(file))
        {
            foreach (var masterPath in dependencyScriptToMonitor[file])
            {
                paths2process.Add(masterPath);
            }
        }

        // We might have files update queue like this:
        //      (1) testScript.cs > (2) dependency.cs
        // Both files - 1 and 2 are updated, but we need for compilation to happen only once.
        foreach (var p in paths2process)
        {
            if (!masterPathsToReload.Contains(p))
            {
                masterPathsToReload.Add(p);
                m_timer.Change(ScriptUpdatedReloadDelay, Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// Traces line in output window
    /// </summary>
    public static void OutputLine(String line)
    {
        if(ConsolePrintLine != null)
            ConsolePrintLine(line);
    }

    /// <summary>
    /// Traces line with source code / line information (double clickable in output window)
    /// </summary>
    public static void TraceLine(
        String line,
        [System.Runtime.CompilerServices.CallerFilePath] string fileName = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0
    )
    {
        Debug.WriteLine(fileName + "(" + lineNumber + "): " + line);
    }

    static void UiFileReload( String file )
    {
        Exception lastException = null;

        for( int iRetry = 0; iRetry < 10; iRetry++ )
        {
            try
            {
                CsScript.RunScript(file);
                break;
            }
            catch (IOException ex)
            {
                // File might be in a middle of save. Try to retry within 50 ms again.
                if (iRetry == 9)
                {
                    lastException = ex;
                    break;
                }
                Thread.Sleep(50);
                continue;
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        if (lastException != null)
            OutputLine(lastException.Message);
    }
}

