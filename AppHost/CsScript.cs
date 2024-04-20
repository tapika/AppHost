using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Xml;
using Process = System.Diagnostics.Process;
using StackFrame = System.Diagnostics.StackFrame;

/// <summary>
/// class for executing c# script.
/// </summary>
public class CsScript
{
    static public string ScriptPath;
    
    static String[] refAssemblies;
    static int loadCounter = 1;

    /// <summary>
    /// 
    /// Compiles .cs script into dll/pdb, loads as assembly, and executes Main function.
    /// 
    /// Assembly gets executed within same appDomain.
    /// 
    /// If there is any compilation error - it will be thrown as exception.
    /// 
    /// Any //css_ref referred file will be loaded into appDomain once, and will stay there until application shuts down.
    /// 
    /// Unfortunately this function will collect .dll & .pdb compilations into %TEMP%\CSScriptHost.
    /// Use CleanupScScriptTempDir() on application startup to wipe out compilation folder.
    /// 
    /// </summary>
    static public void RunScript(string scriptPath = null)
    {
        if(scriptPath == null)
        {
            scriptPath = ScriptPath;
        }
        
        String tempDir = GetScriptTempDir();

        if (!Directory.Exists(tempDir))
            Directory.CreateDirectory(tempDir);

        String path = Path.GetFullPath(scriptPath);
        if( !File.Exists( path ) )
            throw new Exception("Error: Could not load file '" + Path.GetFileName(path) + "': File does not exists.");

        String dllBaseName = Path.GetFileNameWithoutExtension(path) + "_" + loadCounter.ToString();
        String basePath =  Path.Combine(tempDir, dllBaseName);

        String pdbPath = basePath + ".pdb";
        String dllPath = basePath + ".dll";

        try
        {
            List<String> filesToCompile = new List<string>();
            filesToCompile.Add(path);

            //---------------------------------------------------------------------------------------------------
            //  Get referenced .cs script file list, and from referenced files further other referenced files.
            //---------------------------------------------------------------------------------------------------
            CsScriptInfo csInfo = getCsFileInfo(filesToCompile[0]);
            filesToCompile.AddRange(csInfo.csFiles);

            string csproj = Path.Combine(tempDir, $"{Path.GetFileNameWithoutExtension(path)}.csproj");

            // Add assemblies from my domain - all which are not dynamic.
            if (refAssemblies == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).Select(a => a.Location).ToList();
                refAssemblies = assemblies.ToArray();
            }

            if (File.Exists(dllPath))
            { 
                File.Delete(dllPath);
            }

            // We preserve 'obj' directory, as it might cache previous compilation results
            using (XmlTextWriter xml = new XmlTextWriter(csproj, Encoding.UTF8))
            {
                xml.Formatting = Formatting.Indented;
                xml.WriteStartElement("Project");
                xml.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");

                xml.WriteStartElement("PropertyGroup");
                xml.WriteElementString("OutputType", "Library");
                xml.WriteElementString("TargetFramework", "net48");
                xml.WriteElementString("UseWPF", "true");
                //xml.WriteElementString("UseWindowsForms", "true");
                xml.WriteElementString("AppendTargetFrameworkToOutputPath", "false");
                xml.WriteElementString("OutputPath", ".");
                xml.WriteElementString("AssemblyName", Path.GetFileNameWithoutExtension(dllPath));
                xml.WriteElementString("DefineConstants", "TRACE;CS_SCRIPT;DEBUG");     // Do we need DEBUG here ?
                xml.WriteEndElement();

                xml.WriteStartElement("ItemGroup");
                foreach (var srcPath in filesToCompile.Where(x => Path.GetExtension(x).ToLower() == ".cs") )
                {
                    xml.WriteStartElement("Compile");
                    xml.WriteAttributeString("Include", srcPath);
                    xml.WriteAttributeString("Link", Path.GetFileName(srcPath));
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();

                xml.WriteStartElement("ItemGroup");

                foreach (var refDllPath in refAssemblies.Concat(csInfo.refFiles))
                {
                    if (string.IsNullOrEmpty(refDllPath))   // Lib.Harmony generated?
                    {
                        continue;
                    }
                    xml.WriteStartElement("Reference");
                    xml.WriteAttributeString("Include", $"{Path.GetFileName(refDllPath)}");
                    xml.WriteElementString("HintPath", refDllPath);
                    xml.WriteElementString("Private", "false");     // Don't copy .dll locally (available in app folder)
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();

                var xamls = filesToCompile.Where(x => Path.GetExtension(x).ToLower() == ".xaml").ToList();
                if (xamls.Any())
                { 
                    xml.WriteStartElement("ItemGroup");
                    foreach (var xamlPath in xamls)
                    {
                        xml.WriteStartElement("Page");
                        xml.WriteAttributeString("Include", xamlPath);
                        xml.WriteAttributeString("Link", Path.GetFileName(xamlPath));
                        xml.WriteElementString("Generator", "MSBuild:Compile");
                        xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                
                xml.WriteEndElement();
            }

            string dotnetPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\dotnet\dotnet.exe");
            if (!File.Exists(dotnetPath))
            {
                throw new Exception($"Error: dotnet executable does not exists: {dotnetPath}");
            }

            // --no-restore - we don't have any package references, no need to perform nuget restore
            string cmdArgs = $"build \"{csproj}\" --verbosity quiet --nologo -consoleLoggerParameters:NoSummary";
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = dotnetPath,
                UseShellExecute = false,
                Arguments = cmdArgs,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process process = Process.Start(startInfo);
            
            var sb = new StringBuilder();
            process.OutputDataReceived += (sender, args) => { sb.AppendLine(args.Data); };
            process.ErrorDataReceived += (sender, args) => { sb.AppendLine(args.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (!File.Exists(dllPath) || process.ExitCode != 0)
            {
                sb.AppendLine($"While executing command '{dotnetPath} {cmdArgs}'");
                throw new Exception(sb.ToString());
            }

            loadCounter++;

            // ----------------------------------------------------------------
            //  Preload compiled .dll and it's debug information into ram.
            // ----------------------------------------------------------------
            MethodInfo entry = null;
            String funcName = "";
            Assembly asm = Assembly.LoadFrom(dllPath);

            // ----------------------------------------------------------------
            //  Locate entry point
            // ----------------------------------------------------------------
            BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase;

            foreach (Type type in asm.GetTypes())
            {
                funcName = "Main";
                entry = type.GetMethod(funcName, flags);

                if (entry != null)
                    break;
            }

            if (entry == null)
                throw new Exception(String.Format("{0}(1,1): error: Code does not have 'Main' function\r\n", Path.GetFileName(path)));

            if (entry.GetParameters().Length != 0)
                throw new Exception(String.Format("{0}(1,1): error: Function '{1}' is not expected to have no input parameters\r\n", Path.GetFileName(path), funcName));

            String oldDir = Environment.CurrentDirectory;
            //
            // We set current directory to where script is, just so script can use Directory.GetFiles without specifying directory.
            //
            Directory.SetCurrentDirectory(Path.GetDirectoryName(ScriptPath));

            // ----------------------------------------------------------------
            //  Run script
            // ----------------------------------------------------------------
            try
            {
                entry.Invoke(null, new object[] { });
                Directory.SetCurrentDirectory(oldDir);
            }
            catch (Exception ex)
            {
                Directory.SetCurrentDirectory(oldDir);

                String errors = "";

                try
                {
                    StackFrame[] stack = new StackTrace(ex.InnerException, true).GetFrames();
                    StackFrame lastCall = stack[0];

                    errors = String.Format("{0}({1},{2}): error: {3}\r\n", path,
                        lastCall.GetFileLineNumber(), lastCall.GetFileColumnNumber(), ex.InnerException.Message);

                }
                catch (Exception ex3)
                {
                    errors = String.Format("{0}(1,1): error: Internal error - exception '{3}'\r\n", path, ex3.Message);
                }
                throw new Exception(errors);
            }
        }
        finally
        {
            // Will work only if main was not possible to find.
            //try { File.Delete(dllPath); } catch { }

            // Works only when there is no debugger attached.
            //try { File.Delete(pdbPath); } catch { }
        }
    }

    static String GetGlobalScriptTempDir()
    {
        return Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "CSScriptHost");
    }

    static String GetScriptTempDir()
    {
        String exeName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
        // Use process id, so would not conflict.
        return Path.Combine(GetGlobalScriptTempDir(), exeName + "_" + Process.GetCurrentProcess().Id.ToString());
    }

    /// <summary>
    /// Cleans up temporary folder from compiled files.
    /// </summary>
    static public void CleanupScScriptTempDir()
    {
        try
        {
            String scTempDir = GetGlobalScriptTempDir();
            if (!Directory.Exists(scTempDir))
                return;

            String[] dirs = Directory.GetDirectories(scTempDir);
            Regex r = new Regex("_(\\d+)$");
            foreach (String dir in dirs)
            {
                var rr = r.Match(dir);
                if (!rr.Success)
                    continue;

                try
                {
                    Process.GetProcessById(int.Parse(rr.Groups[1].ToString()));
                    continue;
                }
                catch(ArgumentException) { }

                Directory.Delete(dir, true);
            }
        }
        catch
        {
        }
    }


    /// <summary>
    /// Scans through C# script and gets additional information about C# script itself, 
    /// like dependent .cs files, and so on.
    /// </summary>
    /// <param name="csPath">C# script to load and scan</param>
    /// <param name="exceptFiles">Don't include path'es specified in here</param>
    /// <returns>C# script info</returns>
    static public CsScriptInfo getCsFileInfo( String csPath, List<String> exceptFiles = null )
    {
        CsScriptInfo csInfo = new CsScriptInfo();
        if (exceptFiles == null)
            exceptFiles = new List<string>();

        if(!exceptFiles.Contains(csPath) )
            exceptFiles.Add(csPath);

        // ----------------------------------------------------------------
        //  Using C# kind of syntax - like this:
        //      //css_include <file.cs>;
        // ----------------------------------------------------------------
        var regexOpt = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        Regex reIsCommentUsingEmptyLine = new Regex("^ *(//|using|$)", regexOpt);
        Regex reCssImport = new Regex("^ *//css_include +(.*?);?$", regexOpt);
        Regex reCssRef = new Regex("^ *//css_ref +(.*?);?$", regexOpt);

        int iLine = 1;

        List<string> asmSearchDirs = new List<string>();
        // Search .dll's in main executable directory 
        asmSearchDirs.Add(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
        // and next to script
        asmSearchDirs.Add(Path.GetDirectoryName(csPath));

        using (StreamReader reader = new StreamReader(csPath))
        {
            for (; ; iLine++)
            {
                String line = reader.ReadLine();
                if (line == null)
                    break;

                // If we have any comments, or using namespace or empty line, we continue scanning, otherwise aborting (class, etc...)
                if (!reIsCommentUsingEmptyLine.Match(line).Success)
                    break;

                // Pick up .dll filename from //css_ref <dll filename> file line.
                var rasm = reCssRef.Match(line);
                if (rasm.Success)
                {
                    // Allow end user to use %SystemRoot%\....dll environment variables.
                    String file = Environment.ExpandEnvironmentVariables(rasm.Groups[1].Value);
                    string fullpath = file;

                    if (!Path.IsPathRooted(fullpath))
                    {
                        foreach (var dir in asmSearchDirs)
                        {
                            string dllfullpath = Path.Combine(dir, file);
                            if (File.Exists(dllfullpath))
                            { 
                                fullpath = dllfullpath;
                                break;
                            }
                        }
                    }

                    csInfo.refFiles.Add(fullpath);
                    continue;
                }

                var rem = reCssImport.Match(line);
                if (rem.Success)
                {
                    String file = rem.Groups[1].Value;
                    String fileFullPath = file;

                    if (!Path.IsPathRooted(file))
                        fileFullPath = Path.Combine(Path.GetDirectoryName(csPath), file);

                    if (!File.Exists(fileFullPath))
                        throw new ArgumentException("Include file specified in '" + Path.GetFileName(fileFullPath) + 
                            "' was not found (Included from '" + Path.GetFileName(csPath) + "')");

                    bool bContains = false;
                    String fPath;

                    ScriptHost.ObserveScript(csPath, fileFullPath);
                    fPath = fileFullPath;

                    // Prevent cyclic references.
                    bContains = csInfo.csFiles.Contains(fPath);
                    if (!bContains) bContains = exceptFiles.Contains(fileFullPath);

                    if (!bContains)
                    {
                        csInfo.csFiles.Add(fPath.Replace("/", "\\"));
                        exceptFiles.Add(fileFullPath);
                    }

                    if (!bContains)
                    {
                        CsScriptInfo subCsInfo = getCsFileInfo(fileFullPath, exceptFiles);
                        
                        foreach (String subFile in subCsInfo.csFiles)
                            if (!csInfo.csFiles.Contains(subFile))
                                csInfo.csFiles.Add(subFile);

                        foreach (String refFile in subCsInfo.refFiles)
                            if (!csInfo.refFiles.Contains(refFile))
                                csInfo.refFiles.Add(refFile);
                    }

                }
            }
        }

        return csInfo;
    }
}


