using System;
using System.Diagnostics;
using System.IO;
using DotnetMakeDeb.Deb;

namespace DotnetMakeDeb
{
	internal class MakeDeb
	{
		#region Private data

		private bool verboseMode;
		private string specFileName;
		private string versionOverride;
		private string versionFileName;

		#endregion Private data

		#region Execution wrapper

		public int Execute(string[] args)
		{
			try
			{
				return ExecuteInternal(args);
			}
			catch (AppException ex)
			{
				Console.Error.WriteLine(ex.Message);
				return 1;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Error: An unhandled exception has occurred: {ex.Message}");
				return 1;
			}
			finally
			{
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached)
				{
					Console.WriteLine("Press any key to exit...");
					Console.ReadKey(true);
				}
#endif
			}
		}

		#endregion Execution wrapper

		public int ExecuteInternal(string[] args)
		{
			var startTime = DateTime.UtcNow;

			ReadArgs(args);
			if (verboseMode)
				Console.WriteLine($"Specification file: {specFileName}");

			var deb = new DebPackage();
			deb.ReadSpecification(specFileName);

			if (!string.IsNullOrEmpty(versionFileName))
			{
				var fvi = FileVersionInfo.GetVersionInfo(versionFileName);
				deb.SetVersion(fvi.ProductVersion);
				if (verboseMode)
					Console.WriteLine($"Version from file: {fvi.ProductVersion}");
			}
			if (!string.IsNullOrEmpty(versionOverride))
			{
				deb.SetVersion(versionOverride);
				if (verboseMode)
					Console.WriteLine($"Version from command line: {versionOverride}");
			}

			string outDir = deb.OutDir ?? @".\";
			string fileName = Path.Combine(outDir, deb.DefaultFileName);

			if (File.Exists(fileName))
			{
				// Delete existing file or it may be appended to or partially overwritten
				File.Delete(fileName);
			}
			if (deb.OutDir != null)
			{
				// Create specified out dir if it doesn't exist
				Directory.CreateDirectory(deb.OutDir);
			}

			using (FileStream file = File.OpenWrite(fileName))
			{
				deb.WritePackage(file);
			}

			if (verboseMode)
				Console.WriteLine($"Package created in {(DateTime.UtcNow - startTime).TotalSeconds:N2}s.");
			return 0;
		}

		#region Argument handling methods

		private void ReadArgs(string[] args)
		{
			bool optionMode = true;
			Action<string> nextArgHandler = null;   // NOTE: This can only handle a single next argument at a time

			foreach (string arg in args)
			{
				if (nextArgHandler != null)
				{
					nextArgHandler(arg);
					nextArgHandler = null;
				}
				else if (arg == "--")
				{
					optionMode = false;
				}
				else if (optionMode && arg.StartsWith("-"))
				{
					switch (arg.Substring(1))
					{
						case "v":
							verboseMode = true;
							break;
						case "vf":
							nextArgHandler = x => versionFileName = x;
							break;
						default:
							throw new AppException($"Invalid option: {arg}");
					}
				}
				else if (specFileName == null)
				{
					specFileName = arg;
					if (!File.Exists(specFileName))
						throw new AppException($"Specified config file not found: {specFileName}");
				}
				else if (versionOverride == null)
				{
					versionOverride = arg;
				}
				else
				{
					throw new AppException($"Too many arguments: {arg}");
				}
			}
			if (nextArgHandler != null)
			{
				throw new AppException("Missing value after last argument.");
			}
		}

		#endregion Argument handling methods
	}

	#region Application error handling

	internal class AppException : ApplicationException
	{
		public AppException(string message)
			: base(message)
		{
		}

		public AppException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}

	#endregion Application error handling
}
