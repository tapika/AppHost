using HarmonyLib;
using System.Windows;

namespace AppHost
{
    [HarmonyPatch]
    public static class ApplicationPatch
    {
        [HarmonyPatch(typeof(Application), "OnStartup")]
		[HarmonyPostfix]
        static void Postfix()
        {
        }
    }
}
