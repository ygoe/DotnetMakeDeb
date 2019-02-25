using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DotnetMakeDeb.Ar
{
	/// <summary>
	/// Writes UNIX ar (archive) files in the GNU format.
	/// </summary>
	internal class ArWriter
	{
		// Source: http://en.wikipedia.org/wiki/Ar_%28Unix%29

		private readonly Stream outStream;

		/// <summary>
		/// Initialises a new instance of the ArWriter class.
		/// </summary>
		/// <param name="outStream">The stream to write to archive to.</param>
		public ArWriter(Stream outStream)
		{
			this.outStream = outStream;
			Initialize();
		}

		/// <summary>
		/// Writes a file from disk to the archive.
		/// </summary>
		/// <param name="fileName">The name of the file to copy.</param>
		/// <param name="userId">The user ID of the file in the archive.</param>
		/// <param name="groupId">The group ID of the file in the archive.</param>
		/// <param name="mode">The mode of the file in the archive (decimal).</param>
		public void WriteFile(string fileName, int userId = 0, int groupId = 0, int mode = 33188 /* 0100644 */)
		{
			FileInfo fi = new FileInfo(fileName);
			using (FileStream file = File.OpenRead(fileName))
			{
				WriteFile(file, fi.Name, fi.LastWriteTimeUtc, userId, groupId, mode);
			}
		}

		/// <summary>
		/// Writes a file from a Stream to the archive.
		/// </summary>
		/// <param name="stream">The stream to read the file contents from.</param>
		/// <param name="fileName">The name of the file in the archive.</param>
		/// <param name="modifyTime">The last modification time of the file in the archive.</param>
		/// <param name="userId">The user ID of the file in the archive.</param>
		/// <param name="groupId">The group ID of the file in the archive.</param>
		/// <param name="mode">The mode of the file in the archive (decimal).</param>
		public void WriteFile(Stream stream, string fileName, DateTime modifyTime, int userId = 0, int groupId = 0, int mode = 33188 /* 0100644 */)
		{
			// Write file header
			WriteFileHeader(fileName, modifyTime, userId, groupId, mode, stream.Length);

			// Write file contents
			stream.CopyTo(outStream);

			// Align to even offsets, pad with LF bytes
			if ((outStream.Position % 2) != 0)
			{
				byte[] bytes = new byte[] { 0x0A };
				outStream.Write(bytes, 0, 1);
			}
		}

		/// <summary>
		/// Writes the archive header.
		/// </summary>
		private void Initialize()
		{
			WriteAsciiString("!<arch>\n");
		}

		/// <summary>
		/// Writes a file header.
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="modifyTime"></param>
		/// <param name="userId"></param>
		/// <param name="groupId"></param>
		/// <param name="mode"></param>
		/// <param name="fileSize"></param>
		private void WriteFileHeader(string fileName, DateTime modifyTime, int userId, int groupId, int mode, long fileSize)
		{
			// File name
			if (fileName.Length <= 16)
			{
				WriteAsciiString(fileName.PadRight(16, ' '));
			}
			else
			{
				throw new ArgumentException("Long file names are not supported.");
			}

			// File modification timestamp
			int unixTime = (int) (modifyTime - new DateTime(1970, 1, 1)).TotalSeconds;
			WriteAsciiString(unixTime.ToString().PadRight(12, ' '));

			// User ID
			if (userId >= 0)
			{
				WriteAsciiString(userId.ToString().PadRight(6, ' '));
			}
			else
			{
				WriteAsciiString("      ");
			}

			// Group ID
			if (groupId >= 0)
			{
				WriteAsciiString(groupId.ToString().PadRight(6, ' '));
			}
			else
			{
				WriteAsciiString("      ");
			}

			// File mode
			if (mode >= 0)
			{
				WriteAsciiString(Convert.ToString(mode, 8).PadRight(8, ' '));
			}
			else
			{
				WriteAsciiString("        ");
			}

			// File size in bytes
			if (fileSize >= 0 && fileSize < 10000000000)
			{
				WriteAsciiString(fileSize.ToString().PadRight(10, ' '));
			}
			else
			{
				throw new ArgumentOutOfRangeException("Invalid file size.");
			}

			// File magic
			byte[] bytes = new byte[] { 0x60, 0x0A };
			outStream.Write(bytes, 0, 2);
		}

		/// <summary>
		/// Writes a string using ASCII encoding.
		/// </summary>
		/// <param name="str">The string to write to the output stream.</param>
		private void WriteAsciiString(string str)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(str);
			outStream.Write(bytes, 0, bytes.Length);
		}
	}
}
