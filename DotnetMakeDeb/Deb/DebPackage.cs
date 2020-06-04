using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DotnetMakeDeb.Ar;
using DotnetMakeDeb.Tar;

namespace DotnetMakeDeb.Deb
{
	/// <summary>
	/// Creates a Debian binary package.
	/// </summary>
	internal class DebPackage
	{
		private readonly List<string> createdDirs = new List<string>();
		private readonly List<DebFileItem> fileItems = new List<DebFileItem>();
		private readonly Dictionary<string, string> variables = new Dictionary<string, string>();

		private ArWriter arWriter;
		private Stream gzBuffer;
		private GZipStream gzStream;
		private TarWriter tarWriter;
		private DebControlParams controlParams;
		private string preInstFileName;
		private string postInstFileName;
		private string preRmFileName;
		private string postRmFileName;
		private string srcBasePath;

		/// <summary>
		/// Initialises a new instance of the <see cref="DebPackage"/> class.
		/// </summary>
		public DebPackage()
		{
		}

		/// <summary>
		/// Gets the default name of the package file from its control data.
		/// </summary>
		public string DefaultFileName
		{
			get
			{
				if (controlParams != null)
				{
					return controlParams.Package + "_" + controlParams.ConvertedVersion + "_" +
						controlParams.Architecture + ".deb";
				}
				return null;
			}
		}

		/// <summary>
		/// Gets the configured output directory path for the package file, or null.
		/// </summary>
		public string OutDir { get; private set; }

		/// <summary>
		/// Reads the specification file that specifies the contents of the package.
		/// </summary>
		/// <param name="fileName">The name of the package specification file.</param>
		public void ReadSpecification(string fileName)
		{
			srcBasePath = Path.GetDirectoryName(fileName);

			controlParams = new DebControlParams();

			using (var sr = new StreamReader(fileName))
			{
				int lineNumber = 0;
				bool inDescription = false;
				while (!sr.EndOfStream)
				{
					string line = sr.ReadLine();
					lineNumber++;
					Match m;

					if (line.TrimStart() == "")
					{
						inDescription = false;
						continue;   // Skip empty lines
					}
					if (line.TrimStart().StartsWith("#"))
					{
						inDescription = false;
						continue;   // Skip comments
					}

					m = Regex.Match(line, @"^basepath\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						srcBasePath = ResolveVariables(m.Groups[1].Value.Trim());
						if (!Path.IsPathRooted(srcBasePath))
						{
							// Interpret non-rooted paths relative to the spec file
							srcBasePath = Path.Combine(Path.GetDirectoryName(fileName), srcBasePath);
						}
						if (!Directory.Exists(srcBasePath))
						{
							throw new ArgumentException("Specified base path directory does not exist: " + srcBasePath);
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^outdir\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						OutDir = ResolveVariables(m.Groups[1].Value.Trim());
						if (!Path.IsPathRooted(OutDir))
						{
							// Interpret non-rooted paths relative to the spec file
							OutDir = Path.Combine(Path.GetDirectoryName(fileName), OutDir);
						}
						inDescription = false;
						continue;
					}

					m = Regex.Match(line, @"^package\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Package = ResolveVariables(m.Groups[1].Value.Trim());
						// Source: https://www.debian.org/doc/debian-policy/ch-controlfields.html#s-f-package
						if (!Regex.IsMatch(controlParams.Package, "^[a-z0-9+.-]+$"))
						{
							throw new FormatException("Invalid format of the package name.");
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^version\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Version = ResolveVariables(m.Groups[1].Value.Trim());
						// Source: https://www.debian.org/doc/debian-policy/ch-controlfields.html#s-f-version
						if (!Regex.IsMatch(controlParams.Version, "^[A-Za-z0-9.+~-]+$"))
						{
							throw new FormatException("Invalid format of the version number.");
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^convert-semver\s*$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.ConvertFromSemVer = true;
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^section\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Section = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^prio(?:rity)?\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Priority = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^arch(?:itecture)?\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Architecture = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^depends\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						string depends = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						if (string.IsNullOrWhiteSpace(controlParams.Depends))
							controlParams.Depends = depends;
						else
							controlParams.Depends += ", " + depends;
						continue;
					}
					m = Regex.Match(line, @"^pre-depends\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						string preDepends = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						if (string.IsNullOrWhiteSpace(controlParams.PreDepends))
							controlParams.PreDepends = preDepends;
						else
							controlParams.PreDepends += ", " + preDepends;
						continue;
					}
					m = Regex.Match(line, @"^conflicts\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Conflicts = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^maintainer\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Maintainer = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^homepage\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Homepage = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^desc(?:ription)?\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						controlParams.Description = ResolveVariables(m.Groups[1].Value.Trim());
						inDescription = true;
						continue;
					}

					m = Regex.Match(line, @"^preinst\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						preInstFileName = ResolveVariables(m.Groups[1].Value.Trim());
						if (!Path.IsPathRooted(preInstFileName))
						{
							// Interpret non-rooted paths relative to the base path
							preInstFileName = Path.Combine(srcBasePath, preInstFileName);
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^postinst\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						postInstFileName = ResolveVariables(m.Groups[1].Value.Trim());
						if (!Path.IsPathRooted(postInstFileName))
						{
							// Interpret non-rooted paths relative to the base path
							postInstFileName = Path.Combine(srcBasePath, postInstFileName);
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^prerm\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						preRmFileName = ResolveVariables(m.Groups[1].Value.Trim());
						if (!Path.IsPathRooted(preRmFileName))
						{
							// Interpret non-rooted paths relative to the base path
							preRmFileName = Path.Combine(srcBasePath, preRmFileName);
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^postrm\s*:\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						postRmFileName = ResolveVariables(m.Groups[1].Value.Trim());
						if (!Path.IsPathRooted(postRmFileName))
						{
							// Interpret non-rooted paths relative to the base path
							postRmFileName = Path.Combine(srcBasePath, postRmFileName);
						}
						inDescription = false;
						continue;
					}

					m = Regex.Match(line, @"^file\s*:\s*(\S+)\s+(\S+)(?:\s+([0-9]+)(?:\s+([0-9]+)\s+([0-9]+))?)?\s*$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						// Add data file
						var fileItem = new DebFileItem
						{
							SourcePath = ResolveVariables(m.Groups[1].Value.Trim())
						};
						if (!Path.IsPathRooted(fileItem.SourcePath))
						{
							// Interpret non-rooted paths relative to the base path
							fileItem.SourcePath = Path.Combine(srcBasePath, fileItem.SourcePath);
						}
						fileItem.DestPath = ResolveVariables(m.Groups[2].Value.Trim());
						if (m.Groups[3].Length > 0)
							fileItem.Mode = Convert.ToInt32(ResolveVariables(m.Groups[3].Value), 8);
						if (m.Groups[4].Length > 0)
							fileItem.UserId = Convert.ToInt32(ResolveVariables(m.Groups[4].Value));
						if (m.Groups[5].Length > 0)
							fileItem.GroupId = Convert.ToInt32(ResolveVariables(m.Groups[5].Value));
						foreach (var fi in ResolveFileItems(fileItem))
						{
							var existingItem = fileItems.FirstOrDefault(x => x.SourcePath == fi.SourcePath);
							if (existingItem != null)
								fileItems.Remove(existingItem);
							fileItems.Add(fi);
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^conffile\s*:\s*(\S+)\s+(\S+)(?:\s+([0-9]+)(?:\s+([0-9]+)\s+([0-9]+))?)?\s*$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						// Add data file as conffile
						var fileItem = new DebFileItem
						{
							SourcePath = ResolveVariables(m.Groups[1].Value.Trim())
						};
						if (!Path.IsPathRooted(fileItem.SourcePath))
						{
							// Interpret non-rooted paths relative to the base path
							fileItem.SourcePath = Path.Combine(srcBasePath, fileItem.SourcePath);
						}
						fileItem.DestPath = ResolveVariables(m.Groups[2].Value.Trim());
						if (m.Groups[3].Length > 0)
							fileItem.Mode = Convert.ToInt32(ResolveVariables(m.Groups[3].Value), 8);
						if (m.Groups[4].Length > 0)
							fileItem.UserId = Convert.ToInt32(ResolveVariables(m.Groups[4].Value));
						if (m.Groups[5].Length > 0)
							fileItem.GroupId = Convert.ToInt32(ResolveVariables(m.Groups[5].Value));
						fileItem.IsConfig = true;
						foreach (var fi in ResolveFileItems(fileItem))
						{
							var existingItem = fileItems.FirstOrDefault(x => x.SourcePath == fi.SourcePath);
							if (existingItem != null)
								fileItems.Remove(existingItem);
							fileItems.Add(fi);
						}
						inDescription = false;
						continue;
					}
					m = Regex.Match(line, @"^dir(?:ectory)?\s*:\s*(\S+)(?:\s+([0-9]+)(?:\s+([0-9]+)\s+([0-9]+))?)?\s*$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						// Add empty directory
						var fileItem = new DebFileItem
						{
							DestPath = ResolveVariables(m.Groups[1].Value.Trim()).TrimEnd('/')
						};
						if (m.Groups[2].Length > 0)
							fileItem.Mode = Convert.ToInt32(ResolveVariables(m.Groups[2].Value), 8);
						else
							fileItem.Mode = 493 /* 0755 */;
						if (m.Groups[3].Length > 0)
							fileItem.UserId = Convert.ToInt32(ResolveVariables(m.Groups[3].Value));
						if (m.Groups[4].Length > 0)
							fileItem.GroupId = Convert.ToInt32(ResolveVariables(m.Groups[4].Value));
						fileItem.IsDirectory = true;
						fileItems.Add(fileItem);
						inDescription = false;
						continue;
					}

					m = Regex.Match(line, @"^(\S+)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
					if (m.Success)
					{
						variables[m.Groups[1].Value.Trim()] = ResolveVariables(m.Groups[2].Value.Trim());
						inDescription = false;
						continue;
					}

					if (inDescription)
					{
						m = Regex.Match(line, @"^\s+", RegexOptions.IgnoreCase);
						if (m.Success)
						{
							// Continuation line for the Description field, follows the original control file syntax
							controlParams.Description += "\n" + ResolveVariables(line.TrimEnd());
							continue;
						}
					}

					throw new Exception($"Unrecognised directive in line {lineNumber}.");
				}
			}

			if (string.IsNullOrWhiteSpace(controlParams.Package))
				throw new ArgumentException("Package field missing.");
			if (string.IsNullOrWhiteSpace(controlParams.Version))
				throw new ArgumentException("Version field missing.");
			if (string.IsNullOrWhiteSpace(controlParams.Architecture))
				throw new ArgumentException("Architecture field missing.");
			if (string.IsNullOrWhiteSpace(controlParams.Maintainer))
				throw new ArgumentException("Maintainer field missing.");
			if (string.IsNullOrWhiteSpace(controlParams.Description))
				throw new ArgumentException("Description field missing.");

			// Calculate InstalledSize in KiB
			// Source: https://www.debian.org/doc/debian-policy/ch-controlfields.html#s-f-installed-size
			long byteCount = 0;
			foreach (var fileItem in fileItems)
			{
				if (fileItem.SourcePath != null)
				{
					var fi = new FileInfo(fileItem.SourcePath);
					byteCount += fi.Length;
				}
			}
			controlParams.InstalledSize = (long) Math.Ceiling((decimal) byteCount / 1024);
		}

		/// <summary>
		/// Sets the version for the package, overriding the version from the specification file.
		/// </summary>
		/// <param name="version"></param>
		public void SetVersion(string version)
		{
			version = version.Trim();
			// Source: https://www.debian.org/doc/debian-policy/ch-controlfields.html#s-f-version
			if (!Regex.IsMatch(version, "^[A-Za-z0-9.+~-]+$"))
			{
				throw new FormatException("Invalid format of the version number.");
			}
			controlParams.Version = version;
		}

		/// <summary>
		/// Writes the package file.
		/// </summary>
		/// <param name="outStream">The stream to write the package to.</param>
		public void WritePackage(Stream outStream)
		{
			arWriter = new ArWriter(outStream);
			WriteVersion();

			InitializeControl();

			using (Stream controlFile = CreateControlFile())
			{
				AddFile(controlFile, "control", DateTime.UtcNow);
			}
			using (Stream md5sumsFile = CreateMd5sumsFile())
			{
				AddFile(md5sumsFile, "md5sums", DateTime.UtcNow);
			}
			using (Stream conffilesFile = CreateConffilesFile())
			{
				AddFile(conffilesFile, "conffiles", DateTime.UtcNow);
			}
			if (!string.IsNullOrEmpty(preInstFileName))
			{
				AddFile(preInstFileName, 0, 0, 493 /* 0755 */);
			}
			if (!string.IsNullOrEmpty(postInstFileName))
			{
				AddFile(postInstFileName, 0, 0, 493 /* 0755 */);
			}
			if (!string.IsNullOrEmpty(preRmFileName))
			{
				AddFile(preRmFileName, 0, 0, 493 /* 0755 */);
			}
			if (!string.IsNullOrEmpty(postRmFileName))
			{
				AddFile(postRmFileName, 0, 0, 493 /* 0755 */);
			}

			WriteControl();

			InitializeData();

			foreach (var fileItem in fileItems.OrderBy(f => !f.IsDirectory))
			{
				if (fileItem.IsDirectory)
				{
					AddDirectory(fileItem.DestPath, fileItem.UserId, fileItem.GroupId, fileItem.Mode);
				}
				else
				{
					AddFile(fileItem.SourcePath, fileItem.DestPath, fileItem.UserId, fileItem.GroupId, fileItem.Mode);
				}
			}

			WriteData();
		}

		/// <summary>
		/// Writes the version file to the package.
		/// </summary>
		private void WriteVersion()
		{
			var ms = new MemoryStream(Encoding.ASCII.GetBytes("2.0\n"));
			arWriter.WriteFile(ms, "debian-binary", DateTime.UtcNow);
		}

		/// <summary>
		/// Initialises the control archive for the package.
		/// </summary>
		private void InitializeControl()
		{
			gzBuffer = new MemoryStream();
			gzStream = new GZipStream(gzBuffer, CompressionMode.Compress, true);
			tarWriter = new TarWriter(gzStream);
		}

		/// <summary>
		/// Closes and writes the control archive to the package.
		/// </summary>
		private void WriteControl()
		{
			tarWriter.Close();
			gzStream.Close();

			gzBuffer.Seek(0, SeekOrigin.Begin);
			arWriter.WriteFile(gzBuffer, "control.tar.gz", DateTime.UtcNow);
			gzBuffer.Close();
		}

		/// <summary>
		/// Initialises the data archive for the package.
		/// </summary>
		private void InitializeData()
		{
			// NOTE: Optionally allow a file backed stream for this archive if it gets too big
			gzBuffer = new MemoryStream();
			gzStream = new GZipStream(gzBuffer, CompressionMode.Compress, true);
			tarWriter = new TarWriter(gzStream);
		}

		/// <summary>
		/// Closes and writes the data archive to the package.
		/// </summary>
		private void WriteData()
		{
			tarWriter.Close();
			gzStream.Close();

			gzBuffer.Seek(0, SeekOrigin.Begin);
			arWriter.WriteFile(gzBuffer, "data.tar.gz", DateTime.UtcNow);
			gzBuffer.Close();
		}

		/// <summary>
		/// Gets the parent directory of a Unix path, or null.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		private string GetParentDirectory(string path)
		{
			if (path.Contains("/"))
			{
				return Regex.Replace(path, "/[^/]+$", "");
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Ensures that all path segments are added to the archive.
		/// </summary>
		private void EnsureDirectory(string path, int userId = 0, int groupId = 0, int mode = 493 /* 0755 */)
		{
			string parentDir = GetParentDirectory(path);
			if (parentDir != null)
			{
				EnsureDirectory(parentDir, userId, groupId, mode);
				if (!createdDirs.Contains(parentDir))
				{
					AddDirectory(parentDir, userId, groupId, mode, true);
					createdDirs.Add(parentDir);
				}
			}
		}

		/// <summary>
		/// Adds a directory to the currently opened archive.
		/// </summary>
		private void AddDirectory(string path, int userId = 0, int groupId = 0, int mode = 493 /* 0755 */, bool internalCall = false)
		{
			if (!internalCall)
			{
				EnsureDirectory(path, userId, groupId);
			}

			tarWriter.WriteDirectoryEntry(path, userId, groupId, mode);
			createdDirs.Add(path);
		}

		/// <summary>
		/// Adds a file to the currently opened archive.
		/// </summary>
		private void AddFile(string fileName, int userId = 0, int groupId = 0, int mode = 33188 /* 0100644 */)
		{
			var fi = new FileInfo(fileName);
			using (var stream = File.OpenRead(fileName))
			{
				tarWriter.Write(stream, fi.Length, fi.Name, userId, groupId, mode, fi.LastWriteTimeUtc);
			}
		}

		/// <summary>
		/// Adds a file to the currently opened archive.
		/// </summary>
		private void AddFile(string fileName, string destFileName, int userId = 0, int groupId = 0, int mode = 33188 /* 0100644 */)
		{
			EnsureDirectory(destFileName, userId, groupId);

			var fi = new FileInfo(fileName);
			using (var stream = File.OpenRead(fileName))
			{
				tarWriter.Write(stream, fi.Length, destFileName, userId, groupId, mode, fi.LastWriteTimeUtc);
			}
		}

		/// <summary>
		/// Adds a file to the currently opened archive.
		/// </summary>
		private void AddFile(Stream stream, string fileName, DateTime modifyTime, int userId = 0, int groupId = 0, int mode = 33188 /* 0100644 */)
		{
			tarWriter.Write(stream, stream.Length, fileName, userId, groupId, mode, modifyTime);
		}

		/// <summary>
		/// Creates the control file and returns a Stream that contains its data.
		/// </summary>
		/// <returns></returns>
		private Stream CreateControlFile()
		{
			var stream = new MemoryStream();
			WriteUtf8String(stream, $"Package: {controlParams.Package}\n");
			WriteUtf8String(stream, $"Version: {controlParams.ConvertedVersion}\n");
			if (!string.IsNullOrEmpty(controlParams.Architecture))
				WriteUtf8String(stream, $"Architecture: {controlParams.Architecture}\n");
			if (!string.IsNullOrEmpty(controlParams.Depends))
				WriteUtf8String(stream, $"Depends: {controlParams.Depends}\n");
			if (!string.IsNullOrEmpty(controlParams.PreDepends))
				WriteUtf8String(stream, $"Pre-Depends: {controlParams.PreDepends}\n");
			if (controlParams.InstalledSize >= 0)
				WriteUtf8String(stream, $"Installed-Size: {controlParams.InstalledSize}\n");
			if (!string.IsNullOrEmpty(controlParams.Section))
				WriteUtf8String(stream, $"Section: {controlParams.Section}\n");
			if (!string.IsNullOrEmpty(controlParams.Priority))
				WriteUtf8String(stream, $"Priority: {controlParams.Priority}\n");
			if (!string.IsNullOrEmpty(controlParams.Maintainer))
				WriteUtf8String(stream, $"Maintainer: {controlParams.Maintainer}\n");
			if (!string.IsNullOrEmpty(controlParams.Homepage))
				WriteUtf8String(stream, $"Homepage: {controlParams.Homepage}\n");
			if (!string.IsNullOrEmpty(controlParams.Description))
				WriteUtf8String(stream, $"Description: {controlParams.Description}\n");
			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}

		/// <summary>
		/// Creates the md5sums file and returns a Stream that contains its data.
		/// </summary>
		/// <returns></returns>
		private Stream CreateMd5sumsFile()
		{
			var md5 = new MD5CryptoServiceProvider();
			var stream = new MemoryStream();
			foreach (var fileItem in fileItems)
			{
				if (fileItem.SourcePath != null)
				{
					byte[] fileBytes = File.ReadAllBytes(fileItem.SourcePath);
					byte[] hashBytes = md5.ComputeHash(fileBytes);
					string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

					WriteUtf8String(stream, hashString + "  " + fileItem.DestPath + "\n");
				}
			}
			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}

		/// <summary>
		/// Creates the conffiles file and returns a Stream that contains its data.
		/// </summary>
		/// <returns></returns>
		private Stream CreateConffilesFile()
		{
			var stream = new MemoryStream();
			foreach (var fileItem in fileItems)
			{
				if (fileItem.SourcePath != null && fileItem.IsConfig)
				{
					WriteUtf8String(stream, "/" + fileItem.DestPath + "\n");
				}
			}
			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}

		/// <summary>
		/// Writes a string using UTF-8 encoding.
		/// </summary>
		/// <param name="stream">The Stream to write the bytes to.</param>
		/// <param name="str">The string to write to the Stream.</param>
		private void WriteUtf8String(Stream stream, string str)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(str);
			stream.Write(bytes, 0, bytes.Length);
		}

		private string ResolveVariables(string str)
		{
			foreach (var kvp in variables)
			{
				str = str.Replace("{" + kvp.Key + "}", kvp.Value);
			}
			return str;
		}

		private List<DebFileItem> ResolveFileItems(DebFileItem fileItem)
		{
			var fileItems = new List<DebFileItem>();
			if (fileItem.SourcePath.Contains("?") ||
				fileItem.SourcePath.Contains("*"))
			{
				string path = Path.GetDirectoryName(fileItem.SourcePath);
				string searchPattern = Path.GetFileName(fileItem.SourcePath);
				foreach (string fileName in Directory.GetFiles(path, searchPattern))
				{
					var fi = new DebFileItem
					{
						SourcePath = fileName,
						DestPath = fileItem.DestPath.TrimEnd('/') + "/" + Path.GetFileName(fileName),
						UserId = fileItem.UserId,
						GroupId = fileItem.GroupId,
						Mode = fileItem.Mode,
						IsConfig = fileItem.IsConfig
					};
					fileItems.Add(fi);
				}
			}
			else
			{
				if (fileItem.DestPath.EndsWith("/"))
				{
					fileItem.DestPath += Path.GetFileName(fileItem.SourcePath);
				}
				fileItems.Add(fileItem);
			}
			return fileItems;
		}
	}

	/// <summary>
	/// Contains data of a package control file.
	/// </summary>
	internal class DebControlParams
	{
		/// <summary>The name of the package.</summary>
		public string Package;
		/// <summary>The version of the package.</summary>
		public string Version;
		/// <summary>Convert the specified or overridden version from SemVer to Debian conventions.</summary>
		public bool ConvertFromSemVer;
		/// <summary>The section of the package.</summary>
		public string Section;
		/// <summary>The priority of the package.</summary>
		public string Priority;
		/// <summary>The hardware architecture of the package.</summary>
		public string Architecture;
		/// <summary>The package dependencies of the package.</summary>
		public string Depends;
		/// <summary>The package pre-dependencies of the package.</summary>
		public string PreDepends;
		/// <summary>The other packages that the package conflicts with.</summary>
		public string Conflicts;
		/// <summary>The total estimated size of the installed package in KiB.</summary>
		public long InstalledSize;
		/// <summary>The name and e-mail address of the package maintainer in RFC 822 format.</summary>
		public string Maintainer;
		/// <summary>The website URL of the package.</summary>
		public string Homepage;
		/// <summary>The description of the package, in the original control file multi-line syntax.</summary>
		public string Description;

		/// <summary>
		/// Gets the version that was converted from SemVer to Debian, if configured, or the original version.
		/// </summary>
		public string ConvertedVersion
		{
			get
			{
				string version = Version;
				if (ConvertFromSemVer)
				{
					version = Regex.Replace(version, @"^([0-9]+\.[0-9]+\.[0-9]+)-([^-]+)[^.+]*(\.[0-9]+)?(\+[^-]+)?", "$1~$2$3$4");
				}
				return version;
			}
		}
	}

	/// <summary>
	/// Contains data about a file in a package.
	/// </summary>
	internal class DebFileItem
	{
		/// <summary>The full path of the source file to include.</summary>
		public string SourcePath;
		/// <summary>The full path of the destination file to write, not including the root slash.</summary>
		public string DestPath;
		/// <summary>The user ID of the file.</summary>
		public int UserId;
		/// <summary>The group ID of the file.</summary>
		public int GroupId;
		/// <summary>The mode of the file (decimal).</summary>
		public int Mode = 420 /* 0644 */;
		/// <summary>Indicates whether the file is a configuration file.</summary>
		public bool IsConfig;
		/// <summary>Indicates whether the file is a directory.</summary>
		public bool IsDirectory;
	}
}
