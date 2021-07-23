﻿using System;
using System.IO;
using System.Text;
using System.Threading;

namespace DotnetMakeDeb.Tar
{
	public class LegacyTarWriter : IDisposable
	{
		private readonly Stream outStream;
		protected byte[] buffer = new byte[1024];
		private bool isClosed;
		public bool ReadOnZero = true;

		/// <summary>
		/// Writes tar (see GNU tar) archive to a stream
		/// </summary>
		/// <param name="writeStream">stream to write archive to</param>
		public LegacyTarWriter(Stream writeStream)
		{
			outStream = writeStream;
		}

		protected virtual Stream OutStream
		{
			get { return outStream; }
		}

		#region IDisposable Members

		public void Dispose()
		{
			Close();
		}

		#endregion


		public void WriteDirectoryEntry(string path, int userId, int groupId, int mode)
		{
			if(string.IsNullOrEmpty(path))
				throw new ArgumentNullException("path");
			if (path[path.Length - 1] != '/')
			{
				path += '/';
			}
			DateTime lastWriteTime;
			if(Directory.Exists(path))
			{
				lastWriteTime = Directory.GetLastWriteTime(path);
			}
			else
			{
				lastWriteTime = DateTime.Now;
			}

			// handle long path names (> 99 characters)
			if (path.Length > 99)
			{
				WriteLongName(
					name: path,
					userId: userId,
					groupId: groupId,
					mode: mode,
					lastModificationTime: lastWriteTime);

				// shorten the path name so it can be written properly
				path = path.Substring(0, 99);
			}

			WriteHeader(path, lastWriteTime, 0, userId, groupId, mode, EntryType.Directory);
		}

		public void WriteDirectory(string directory, bool doRecursive)
		{
			if (string.IsNullOrEmpty(directory))
				throw new ArgumentNullException("directory");

			WriteDirectoryEntry(directory, 0, 0, 0755);

			string[] files = Directory.GetFiles(directory);
			foreach(var fileName in files)
			{
				Write(fileName);
			}

			string[] directories = Directory.GetDirectories(directory);
			foreach(var dirName in directories)
			{
				WriteDirectoryEntry(dirName, 0, 0, 0755);
				if(doRecursive)
				{
					WriteDirectory(dirName,true);
				}
			}
		}


		public void Write(string fileName)
		{
			if(string.IsNullOrEmpty(fileName))
				throw new ArgumentNullException("fileName");
			using (FileStream file = File.OpenRead(fileName))
			{
				Write(file, file.Length, fileName, 61, 61, 511, File.GetLastWriteTime(file.Name));
			}
		}

		public void Write(FileStream file)
		{
			string path = Path.GetFullPath(file.Name).Replace(Path.GetPathRoot(file.Name),string.Empty);
			path = path.Replace(Path.DirectorySeparatorChar, '/');
			Write(file, file.Length, path, 61, 61, 511, File.GetLastWriteTime(file.Name));
		}

		public void Write(Stream data, long dataSizeInBytes, string name)
		{
			Write(data, dataSizeInBytes, name, 61, 61, 511, DateTime.Now);
		}

		public virtual void Write(string name, long dataSizeInBytes, int userId, int groupId, int mode, DateTime lastModificationTime, WriteDataDelegate writeDelegate)
		{
			IArchiveDataWriter writer = new DataWriter(OutStream, dataSizeInBytes);
			WriteHeader(name, lastModificationTime, dataSizeInBytes, userId, groupId, mode, EntryType.File);
			while(writer.CanWrite)
			{
				writeDelegate(writer);
			}
			AlignTo512(dataSizeInBytes, false);
		}

		public virtual void Write(Stream data, long dataSizeInBytes, string name, int userId, int groupId, int mode,
								  DateTime lastModificationTime)
		{
			if(isClosed)
				throw new TarException("Can not write to the closed writer");

			// handle long file names (> 99 characters)
			if (name.Length > 99)
			{
				WriteLongName(
					name: name,
					userId: userId,
					groupId: groupId,
					mode: mode,
					lastModificationTime: lastModificationTime);

				// shorten the file name so it can be written properly
				name = name.Substring(0, 99);
			}

			WriteHeader(name, lastModificationTime, dataSizeInBytes, userId, groupId, mode, EntryType.File);
			WriteContent(dataSizeInBytes, data);
			AlignTo512(dataSizeInBytes,false);
		}

		/// <summary>
		/// Handle long file or path names (> 99 characters).
		/// Write header and content, which its content contain the long (complete) file/path name.
		/// <para>This handling method is adapted from https://github.com/qmfrederik/dotnet-packaging/pull/50/files#diff-f64c58cc18e8e445cee6ffed7a0d765cdb442c0ef21a3ed80bd20514057967b1 </para>
		/// </summary>
		/// <param name="name">File name or path name.</param>
		/// <param name="userId">User ID.</param>
		/// <param name="groupId">Group ID.</param>
		/// <param name="mode">Mode.</param>
		/// <param name="lastModificationTime">Last modification time.</param>
		private void WriteLongName(string name, int userId, int groupId, int mode, DateTime lastModificationTime)
		{
			// must include a trailing \0
			var nameLength = Encoding.UTF8.GetByteCount(name);
			byte[] entryName = new byte[nameLength + 1];

			Encoding.UTF8.GetBytes(name, 0, name.Length, entryName, 0);

			// add a "././@LongLink" pseudo-entry which contains the full name
			using (var nameStream = new MemoryStream(entryName))
			{
				WriteHeader("././@LongLink", lastModificationTime, entryName.Length, userId, groupId, mode, EntryType.LongName);
				WriteContent(entryName.Length, nameStream);
				AlignTo512(entryName.Length, false);
			}
		}

		protected void WriteContent(long count, Stream data)
		{
			while (count > 0 && count > buffer.Length)
			{
				int bytesRead = data.Read(buffer, 0, buffer.Length);
				if (bytesRead < 0)
					throw new IOException("LegacyTarWriter unable to read from provided stream");
				if (bytesRead == 0)
				{
					if (ReadOnZero)
						Thread.Sleep(100);
					else
						break;
				}
				OutStream.Write(buffer, 0, bytesRead);
				count -= bytesRead;
			}
			if (count > 0)
			{
				int bytesRead = data.Read(buffer, 0, (int) count);
				if (bytesRead < 0)
					throw new IOException("LegacyTarWriter unable to read from provided stream");
				if (bytesRead == 0)
				{
					while (count > 0)
					{
						OutStream.WriteByte(0);
						--count;
					}
				}
				else
					OutStream.Write(buffer, 0, bytesRead);
			}
		}

		protected virtual void WriteHeader(string name, DateTime lastModificationTime, long count, int userId, int groupId, int mode, EntryType entryType)
		{
			var header = new TarHeader
						 {
							 FileName = name,
							 LastModification = lastModificationTime,
							 SizeInBytes = count,
							 UserId = userId,
							 GroupId = groupId,
							 Mode = mode,
							 EntryType = entryType
						 };
			OutStream.Write(header.GetHeaderValue(), 0, header.HeaderSize);
		}


		public void AlignTo512(long size,bool acceptZero)
		{
			size = size%512;
			if (size == 0 && !acceptZero) return;
			while (size < 512)
			{
				OutStream.WriteByte(0);
				size++;
			}
		}

		public virtual void Close()
		{
			if (isClosed) return;
			AlignTo512(0,true);
			AlignTo512(0,true);
			isClosed = true;
		}
	}
}
