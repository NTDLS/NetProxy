using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// Lists a folder on the machine that the service runs on, so that a console on another machine can pick a certificate, a plugin
    /// or a log folder there. Only names, sizes and dates are returned; the content of a file is never read.
    /// </summary>
    public class QueryBrowse : IRmQuery<QueryBrowseReply>
    {
        /// <summary>
        /// The folder to list. Empty lists the drives (Windows) or the root folder (Linux). A relative path is relative to the folder that
        /// the service is installed in. The path of a file lists the folder that it is in, with the file selected. A path that does not
        /// exist lists the nearest folder above it that does.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Only the files with one of these extensions (".pfx") are listed. Empty lists every file.
        /// </summary>
        public List<string> Extensions { get; set; } = new();

        /// <summary>
        /// List the folders only, for choosing a folder.
        /// </summary>
        public bool FoldersOnly { get; set; }
    }

    public class NpBrowseEntry
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedUtc { get; set; }
    }

    public class QueryBrowseReply : IRmQueryReply
    {
        public string? Message { get; set; }

        /// <summary>The folder that was listed (empty for the list of drives).</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>The folder above it, which is empty for the list of drives, or null when there is none (the root on Linux).</summary>
        public string? Parent { get; set; }

        /// <summary>The name of the file that was asked for, when the path was one.</summary>
        public string? Selected { get; set; }

        public List<NpBrowseEntry> Entries { get; set; } = new();

        /// <summary>The list was cut short because the folder holds too many entries.</summary>
        public bool Truncated { get; set; }
    }
}
