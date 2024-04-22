using HarmonyLib;
using System;
using System.Linq;
using System.Windows;

namespace AppHost
{
    [HarmonyPatch]
    static class ApplicationPatch
    {
        delegate object GetConsoleInstance();
        static GetConsoleInstance _getConsoleInstance = null;

        [HarmonyPatch(typeof(Application), "OnStartup")]
		[HarmonyPostfix]
        static void Postfix()
        {
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            var iprinters = asms.SelectMany(x => x.GetTypes()).Where(x => x.GetInterfaces().Where(i => i.Name == "IConsoleViewModel").FirstOrDefault() != null).ToArray();

            if (iprinters.Length == 1)
            {
                Type printterClassType = iprinters[0];
                Type IConsoleViewModelType = printterClassType.GetInterfaces().Where(i => i.Name == "IConsoleViewModel").FirstOrDefault();
                var calibburnAsm = asms.Where(x => x.GetName().Name == "Caliburn.Micro").FirstOrDefault();

                // If main application uses Caliburn.Micro, we can query implementation interface via it, otherwise just assume it's main window
                // which handles event. We need some knowledge on application indeed.
                if (calibburnAsm != null)
                {
                    var func = (Func<Type, string, object>)calibburnAsm.GetType("Caliburn.Micro.IoC").
                        GetField("GetInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).GetValue(null);

                    _getConsoleInstance = () =>
                    {
                        return func(IConsoleViewModelType, null);
                    };
                }
                else
                { 
                    _getConsoleInstance = () =>
                    {
                        return Application.Current.MainWindow;
                    };
                }

                var appendMeth = printterClassType.GetMethod("AppendError", new Type[] { typeof(String) });
                if(appendMeth != null)
                {
                    ScriptHost.ConsolePrintLine = (string line) =>
                    {
                        appendMeth.Invoke(_getConsoleInstance(), new object[] { line });
                    };
                }
                
                var clearMeth = printterClassType.GetMethod("Clear", new Type[] { });
                if (clearMeth != null)
                {
                    ScriptHost.ConsoleClear = () =>
                    {
                        clearMeth.Invoke(_getConsoleInstance(), new object[] { });
                    };
                }
            }

            ScriptHost.ObserveScript(CsScript.ScriptPath);
        }
    }
}
