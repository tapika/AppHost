# Overview

`AppHost` is an example application which can modify oritinal executable run-time behavior by bringing additional features support into application.

As a demo we bring C# scripting support, where test script can be found from `TestScript\Program.cs`.

`AppHost` currently works only with WPF Application based on .net 4.72/4.8 framework.

C# script is similar to `CS-Script`, except made completely manually from scratch.

## Technical limitations

I've made this sample just to test .net framework technology limitations and possibilities. .net core allows assembly unload, but not .net framework unfortunately.
In this demo - C# scripts are compiled into temporary folder and assembly name always changes, for example `Program_1.dll > Program_2.dll > ` and so on.

Approach like this floods current appdomain, but for quick C# / .net capabilities testing it's maybe not bad.

## Configuring AppHost

In `TargetHostExe.props` there is `TargetHostExePath` configuration parameter which defines where AppHost will be compiled.
Better to use absolute path with your own executable file.

`App.Config` defines `Script` parameter from where script will be launched. Also recommended to use absolute path, unless it's located relatively to main executable.

When doing final deployment - it's sufficient to copy AppHost.exe & .config to main application folder, so it can be started from there.

## Referencing external Assemblies

From C# script perspective all assemblies are read-only - you can use them, but cannot modify them - so reload works only for C# script itself.

You can add lines like this to reference external assemblies:

```
//css_ref additionalassembly.dll
```

To have intellisense in Visual studio working as well - you can add same assemblies from project itself.

By default testScript will reference all assemblies present in application (`AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)`), so this additional line might be unnecesary normally.

## Referencing external source codes

You can reference external source codes as well, for example like this:

```
//css_include ../../test_.../sharedcode.cs
```

Same file needs to be added to testScript project if you wish to compile project itself in Visual studio.

Application will track down modifications done to those files and will recompile main script (`testScript.cs`) once dependent module changes.

### Using C# script for UI development

You can add `MainWindow.xaml` into test script project - for example like this:

```
<Window x:Class="testScript.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:testScript"
        mc:Ignorable="d"
        Title="Main window" Height="200" Width="500">
    <Grid>
        <Button Margin="107,27,109,34" x:Name="button1" Click="OnClick" Content="Say Hello" />
    </Grid>
</Window>
```

And then refer xaml from script itself, for example like this:

```
//css_include MainWindow.xaml
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace testScript
{

public partial class MainWindow: Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    void OnClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Hello World");
    }

    public static void Main()
    {
        string scriptFilename = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        ScriptHost.OutputLine("Executing: " + scriptFilename);

        MainWindow wnd = new MainWindow();
        wnd.ShowDialog();

        ScriptHost.OutputLine("Closed.");
    }
}

}
```

## Mapping to hotreload

See also following external links:

* [Introducing the .NET Hot Reload experience for editing code at runtime](https://devblogs.microsoft.com/dotnet/introducing-net-hot-reload/)

* [Write and debug running code with Hot Reload in Visual Studio (C#, Visual Basic, C++)](https://learn.microsoft.com/en-us/visualstudio/debugger/hot-reload?view=vs-2022)

* [Troubleshooting XAML Hot Reload](https://learn.microsoft.com/en-us/visualstudio/xaml-tools/xaml-hot-reload-troubleshooting?view=vs-2022#known-limitations)

Hot reload feature seems to be working for .net 4.8 when native debugging is not enabled. (latest vs2022, Debug configuration). Xaml hotreload works or does not work depending on xaml.

