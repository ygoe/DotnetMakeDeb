<img src="https://raw.githubusercontent.com/ygoe/DotnetMakeDeb/master/Logo/DotnetMakeDeb.png" width="128" height="128" alt="DotnetMakeDeb logo">

# dotnet-make-deb

Creates a .deb Debian binary package from a specification file through the dotnet CLI command or as standalone command-line tool.

[![NuGet](https://img.shields.io/nuget/v/Unclassified.DotnetMakeDeb.svg)](https://www.nuget.org/packages/Unclassified.DotnetMakeDeb)

## Installation

### dotnet

Install the NuGet package **Unclassified.DotnetMakeDeb** to your **.NET Core 2.0** or **.NET Standard 2.0** project in VS 2017 or later as `DotNetCliToolReference`. Then you can run it from the project directory to create your Debian package.

.csproj example: (be sure to use the latest version)

    <ItemGroup>
      <DotNetCliToolReference Include="Unclassified.DotnetMakeDeb" Version="1.0.0"/>
    </ItemGroup>

Command invocation: (only in the project directory)

    dotnet publish -c Release
    dotnet make-deb app.debspec

### standalone

To use this tool in other environments than dotnet projects, use the separate standalone console application. It’s a single executable that depends on the .NET Framework 4.6.1 or later. You can place this program file somewhere in your %PATH% so you can quickly run it from all your projects. But you can simply save it in your project directory as well. It is invoked similarly and accepts all the same command line options:

    make-deb app.debspec

You will also need a package specification file which is described in the separate document [MakeDeb.html](https://htmlpreview.github.io/?https://github.com/ygoe/DotnetMakeDeb/blob/master/MakeDeb.html).

## Building

You can build this solution in Visual Studio or by running the command:

    build.cmd

Visual Studio 2017 (15.3) or later with .NET Core 2.0 support is required to build this solution.

## Package version

The package version is normally specified in the package specification file (.debspec).

Alternatively, the version can be overridden from a second command line argument after the specification file:

    [dotnet] make-deb app.debspec 1.2.0

In automated build scenarios, the package version can also be looked up from another file, like the built application assembly. Use the `-vf` option to specify the file to read the version from:

    [dotnet] make-deb app.debspec -vf bin\Release\netcoreapp3.0\linux-x64\publish\MyApp.dll

## Other options

The `-v` option activates verbose output. It prints some progress information while running. Otherwise, it remains silent in normal operation.

## License

[MIT license](https://github.com/ygoe/DotnetMakeDeb/blob/master/LICENSE)
