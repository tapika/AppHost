using HarmonyLib;
using System.Windows;

namespace AppHost
{
    [HarmonyPatch]
    static class ApplicationPatch
    {
        [HarmonyPatch(typeof(Application), "OnStartup")]
		[HarmonyPostfix]
        static void Postfix()
        {
            ScriptHost.ObserveScript(CsScript.ScriptPath);
        }
    }
}
