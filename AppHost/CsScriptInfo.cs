using System;
using System.Collections.Generic;

/// <summary>
/// Additional info about c# script.
/// </summary>
public class CsScriptInfo
{
    /// <summary>
    /// Referred .cs files to include into compilation
    /// </summary>
    public List<String> csFiles = new List<string>();

    /// <summary>
    /// Referred .dll's and assembly names, which must be included as reference assemblies when compiling.
    /// </summary>
    public List<String> refFiles = new List<string>();
}
