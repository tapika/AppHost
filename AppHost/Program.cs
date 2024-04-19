using HarmonyLib;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace AppHost
{
    internal class Program
    {
        [STAThread] //If not present, can result in exception: 'GetExportedValue cannot be called before prerequisite import ... has been set.'
        static int Main(string[] args)
        {
            MethodInfo entry = null;
            string TargetHostExePath = ConfigurationManager.AppSettings["HostExe"];
            string scriptPath  = ConfigurationManager.AppSettings["Script"];
            if (!String.IsNullOrEmpty(scriptPath) && !Path.IsPathRooted(scriptPath))
            {
                scriptPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), scriptPath);
            }
            CsScript.CleanupScScriptTempDir();
            CsScript.ScriptPath = scriptPath;
            var dispatcher = Dispatcher.CurrentDispatcher;
            ScriptHost.OnUIThread = (Action action) =>
            {
                dispatcher.Invoke(action);
            };

            if (String.IsNullOrEmpty(TargetHostExePath) || !File.Exists(TargetHostExePath))
            {
                MessageBox.Show("Please reconfigure 'HostExe' value in AppHost.config file to point to valid executable");
                return 0;
            }
            string baseDir = Path.GetDirectoryName(TargetHostExePath);

            try
            {
                var asm = Assembly.LoadFrom(TargetHostExePath);

                var appType = asm.GetTypes().Where(x => typeof(Application).IsAssignableFrom(x)).FirstOrDefault();
                if (appType == null)
                {
                    MessageBox.Show($"Executable '{Path.GetFileName(TargetHostExePath)}' is not WPF application", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return 0;
                }

                // Corresponds to: "Application.ResourceAssembly = asm;"
                // See https://stackoverflow.com/questions/12429917/load-wpf-application-from-the-memory
                var app = typeof(Application);
                app.GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, asm);

                var appConfig = Path.Combine(baseDir, TargetHostExePath + ".config");
                if(File.Exists(appConfig))
                {
                    AppConfig.Change(appConfig);
                }

                entry = appType.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
            }
            catch (Exception)
            {
                return -6;
            }

            var harmonyInstance = new Harmony("AppHost");
            var processor = harmonyInstance.CreateClassProcessor(typeof(ApplicationPatch));
            processor.Patch();

            // Run without try-catch handler - this is intentional - give application to handle it's own unhandled exceptions.
            if (entry != null)
            {
                // Main returns void, but exit code is preserved in Environment.ExitCode
                entry.Invoke(null, new object[] { });
            }

            return Environment.ExitCode;
        }
    }
}
