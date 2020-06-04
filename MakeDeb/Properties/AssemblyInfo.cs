using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("MakeDeb")]

[assembly: AssemblyProduct("Make a Debian package")]
[assembly: AssemblyDescription("Creates a .deb Debian binary package from a specification file.")]
[assembly: AssemblyCompany("unclassified software development")]
[assembly: AssemblyCopyright("© {copyright:2019-} Yves Goergen")]

// Assembly identity version. Must be a dotted-numeric version.
[assembly: AssemblyVersion("0.0.1")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("0.0.1")]

[assembly: AssemblyInformationalVersion("{semvertag+chash}")]

// Indicate the build configuration
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

// Other attributes
[assembly: ComVisible(false)]
