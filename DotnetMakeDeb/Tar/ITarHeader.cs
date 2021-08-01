using System;

namespace DotnetMakeDeb.Tar
{
    /// <summary>
    /// See "Values used in typeflag field." in https://www.gnu.org/software/tar/manual/html_node/Standard.html
    /// </summary>
    public enum EntryType : byte
    {
        /// <summary>
        /// AREGTYPE, regular file
        /// </summary>
        File = 0,

        /// <summary>
        /// REGTYPE, regular file
        /// </summary>
        FileObsolete = 0x30,

        /// <summary>
        /// LNKTYPE, link
        /// </summary>
        HardLink = 0x31,

        /// <summary>
        /// SYMTYPE, reserved
        /// </summary>
        SymLink = 0x32,

        /// <summary>
        /// CHRTYPE, character special
        /// </summary>
        CharDevice = 0x33,

        /// <summary>
        /// BLKTYPE, block special
        /// </summary>
        BlockDevice = 0x34,

        /// <summary>
        /// DIRTYPE, directory
        /// </summary>
        Directory = 0x35,

        /// <summary>
        /// FIFOTYPE, FIFO special
        /// </summary>
        Fifo = 0x36,

        /// <summary>
        /// CONTTYPE, reserved
        /// </summary>
        Content = 0x37,

        /// <summary>
        /// XHDTYPE, Extended header referring to the next file in the archive
        /// </summary>
        ExtendedHeader = 0x78,

        /// <summary>
        /// XGLTYPE, Global extended header
        /// </summary>
        GlobalExtendedHeader = 0x67,

        /// <summary>
        /// GNUTYPE_LONGLINK, Identifies the *next* file on the tape as having a long linkname.
        /// </summary>
        LongLink = 0x4b,

        /// <summary>
        /// GNUTYPE_LONGNAME, Identifies the *next* file on the tape as having a long name.
        /// </summary>
        LongName = 0x4c
    }

    /// <summary>
    /// See "struct star_header" in https://www.gnu.org/software/tar/manual/html_node/Standard.html
    /// </summary>
    public interface ITarHeader
    {
        /// <summary>
        /// <para>name</para>
        /// <para>byte offset: 0</para>
        /// The name field is the file name of the file, with directory names (if any) preceding the file name,
        /// separated by slashes.
        /// </summary>
        string FileName { get; set; }

        /// <summary>
        /// <para>mode</para>
        /// <para>byte offset: 100</para>
        /// The mode field provides nine bits specifying file permissions and three bits to specify
        /// the Set UID, Set GID, and Save Text (sticky) modes.
        /// When special permissions are required to create a file with a given mode,
        /// and the user restoring files from the archive does not hold such permissions,
        /// the mode bit(s) specifying those special permissions are ignored.
        /// Modes which are not supported by the operating system restoring files from the archive will be ignored.
        /// Unsupported modes should be faked up when creating or updating an archive; e.g.,
        /// the group permission could be copied from the other permission.
        /// </summary>
        int Mode { get; set; }

        /// <summary>
        /// <para>uid</para>
        /// <para>byte offset: 108</para>
        /// The uid field is the numeric user ID of the file owners.
        /// If the operating system does not support numeric user ID, this field should be ignored.
        /// </summary>
        int UserId { get; set; }

        /// <summary>
        /// <para>gid</para>
        /// <para>byte offset: 116</para>
        /// The gid fields is the numeric group ID of the file owners.
        /// If the operating system does not support numeric group ID, this field should be ignored.
        /// </summary>
        int GroupId { get; set; }

        /// <summary>
        /// <para>size</para>
        /// <para>byte offset: 124</para>
        /// The size field is the size of the file in bytes;
        /// linked files are archived with this field specified as zero.
        /// </summary>
        long SizeInBytes { get; set; }

        /// <summary>
        /// <para>mtime</para>
        /// <para>byte offset: 136</para>
        /// The mtime field represents the data modification time of the file at the time it was archived.
        /// It represents the integer number of seconds since January 1, 1970, 00:00 Coordinated Universal Time.
        /// </summary>
        DateTime LastModification { get; set; }

        /// <summary>
        /// <para>typeflag</para>
        /// <para>byte offset: 156</para>
        /// The typeflag field specifies the type of file archived.
        /// If a particular implementation does not recognize or permit the specified type,
        /// the file will be extracted as if it were a regular file.
        /// As this action occurs, tar issues a warning to the standard error.
        /// </summary>
        EntryType EntryType { get; set; }

        /// <summary>
        /// <para>uname</para>
        /// <para>byte offset: 265</para>
        /// The uname field will contain the ASCII representation of the owner of the file.
        /// If found, the user ID is used rather than the value in the uid field.
        /// </summary>
        string UserName { get; set; }

        /// <summary>
        /// <para>gname</para>
        /// <para>byte offset: 297</para>
        /// The gname field will contain the ASCII representation of the group of the file.
        /// If found, the group ID is used rather than the values in the gid field.
        /// </summary>
        string GroupName { get; set; }

        int HeaderSize { get; }
    }
}
