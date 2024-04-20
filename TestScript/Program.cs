namespace TestScript
{
    internal class Program
    {
        static void Main()
        {
            ScriptHost.ConsoleClear();
            ScriptHost.OutputLine("--------------------------------------------------------------------------------");
            ScriptHost.OutputLine("Hello world from C# script");
            ScriptHost.OutputLine("C# script gets dynamically compiled by AppHost, first time when application");
            ScriptHost.OutputLine("is launched, and next time when script itself gets modified");
            ScriptHost.OutputLine("--------------------------------------------------------------------------------");
            ScriptHost.OutputLine("Try to modify TestScript's Program.cs and see result.");
        }
    }
}
