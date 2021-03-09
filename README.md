<img src="https://raw.githubusercontent.com/ygoe/DotnetMakeDeb/master/Logo/DotnetMakeDeb.png" width="128" height="128" alt="DotnetMakeDeb logo">

# dotnet-make-deb

Creates a .deb Debian binary package from a specification file through the dotnet CLI command or as standalone command-line tool.

[![NuGet](https://img.shields.io/nuget/v/Unclassified.DotnetMakeDeb.svg)](https://www.nuget.org/packages/Unclassified.DotnetMakeDeb)

## Installation

### dotnet local tool

Install the NuGet package **Unclassified.DotnetMakeDeb** to your project directory. Then you can run it from the project directory to create your Debian package. This requires the [.NET 5.0 runtime](https://dotnet.microsoft.com/download) to be installed.

Installation:

    dotnet new tool-manifest
    dotnet tool install Unclassified.DotnetMakeDeb

Command invocation:

    dotnet publish -c Release
    dotnet make-deb app.debspec

### dotnet global tool

Install the NuGet package **Unclassified.DotnetMakeDeb** as a global tool. Then you can run it from all directories to create your Debian package. This requires the [.NET 5.0 runtime](https://dotnet.microsoft.com/download) to be installed.

Installation:

    dotnet tool install -g Unclassified.DotnetMakeDeb

Command invocation:

    make-deb app.debspec

[Learn more about managing .NET tools.](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools)

### standalone

To use this tool in other environments than dotnet projects and without a dependency on the new .NET runtime, use the separate standalone console application. It’s a single executable that depends on the .NET Framework 4.6.1 or later. You can place this program file somewhere in your %PATH% so you can quickly run it from all your projects. But you can simply save it in your project directory as well. It is invoked similarly and accepts all the same command line options:

    make-deb app.debspec

You will also need a package specification file which is described in the separate document [MakeDeb.html](https://htmlpreview.github.io/?https://github.com/ygoe/DotnetMakeDeb/blob/master/MakeDeb.html).

## Package version

The package version is normally specified in the package specification file (.debspec).

Alternatively, the version can be overridden from a second command line argument after the specification file:

    [dotnet] make-deb app.debspec 1.2.0

In automated build scenarios, the package version can also be looked up from another file, like the built application assembly. Use the `-vf` option to specify the file to read the version from:

    [dotnet] make-deb app.debspec -vf bin\Release\netcoreapp3.0\linux-x64\publish\MyApp.dll

## Other options

The `-v` option activates verbose output. It prints some progress information while running. Otherwise, it remains silent in normal operation.

## Building

You can build this solution in Visual Studio or by running the command:

    build.cmd

### Requirements

Visual Studio 2019 or later with .NET 5.0 support is required to build this solution.

## License

[MIT license](https://github.com/ygoe/DotnetMakeDeb/blob/master/LICENSE)
