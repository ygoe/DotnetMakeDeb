using System;
using System.IO;

namespace DotnetMakeDeb.Tar
{
    public class TarWriter : LegacyTarWriter
    {

        public TarWriter(Stream writeStream) : base(writeStream)
        {
        }

        protected override void WriteHeader(string name, DateTime lastModificationTime, long count, int userId, int groupId, int mode, EntryType entryType)
        {
            var tarHeader = new UsTarHeader()
            {
                FileName = name,
                Mode = mode,
                UserId = userId,
                GroupId = groupId,
                SizeInBytes = count,
                LastModification = lastModificationTime,
                EntryType = entryType,
                UserName = Convert.ToString(userId,8),
                GroupName = Convert.ToString(groupId,8)
            };
            OutStream.Write(tarHeader.GetHeaderValue(), 0, tarHeader.HeaderSize);
        }

        protected virtual void WriteHeader(string name, DateTime lastModificationTime, long count, string userName, string groupName, int mode, EntryType entryType)
        {
            WriteHeader(
                name: name,
                lastModificationTime: lastModificationTime,
                count: count,
                userId: userName.GetHashCode(),
                groupId: groupName.GetHashCode(),
                mode: mode,
                entryType: entryType);
        }


        public virtual void Write(string name, long dataSizeInBytes, string userName, string groupName, int mode, DateTime lastModificationTime, WriteDataDelegate writeDelegate)
        {
            var writer = new DataWriter(OutStream,dataSizeInBytes);
            WriteHeader(name, lastModificationTime, dataSizeInBytes, userName, groupName, mode, EntryType.File);
            while(writer.CanWrite)
            {
                writeDelegate(writer);
            }
            AlignTo512(dataSizeInBytes, false);
        }


        public void Write(Stream data, long dataSizeInBytes, string fileName, string userId, string groupId, int mode,
                          DateTime lastModificationTime)
        {
            WriteHeader(fileName,lastModificationTime,dataSizeInBytes,userId, groupId, mode, EntryType.File);
            WriteContent(dataSizeInBytes,data);
            AlignTo512(dataSizeInBytes,false);
        }
    }
}
