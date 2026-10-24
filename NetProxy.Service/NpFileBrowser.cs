using NetProxy.Library.Payloads.ReliableMessages.Queries;

namespace NetProxy.Service
{
    /// <summary>
    /// Lists folders for the file picker of the console. It reads names, sizes and dates and nothing else.
    /// </summary>
    internal static class NpFileBrowser
    {
        private const int MaximumEntries = 5000;

        public static QueryBrowseReply Browse(QueryBrowse query, string baseDirectory)
        {
            var reply = new QueryBrowseReply();

            try
            {
                var path = (query.Path ?? string.Empty).Trim().Trim('"');

                if (path.Length == 0)
                {
                    ListRoots(reply, query);
                    return reply;
                }

                if (Path.IsPathRooted(path) == false)
                {
                    path = Path.Combine(baseDirectory, path);
                }

                path = Path.GetFullPath(path);
                if (string.Equals(Path.GetPathRoot(path), path, StringComparison.Ordinal) == false)
                {
                    path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                if (File.Exists(path))
                {
                    reply.Selected = Path.GetFileName(path);
                    path = Path.GetDirectoryName(path) ?? string.Empty;
                }

                //A path that does not exist (yet) is the nearest folder above it that does.
                while (path.Length > 0 && Directory.Exists(path) == false)
                {
                    path = Path.GetDirectoryName(path) ?? string.Empty;
                }

                if (path.Length == 0)
                {
                    reply.Selected = null;
                    ListRoots(reply, query);
                    return reply;
                }

                ListFolder(reply, query, path);
            }
            catch (Exception ex)
            {
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        private static void ListRoots(QueryBrowseReply reply, QueryBrowse query)
        {
            reply.Path = string.Empty;
            reply.Parent = null;

            if (OperatingSystem.IsWindows())
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.IsReady == false)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    var label = string.Empty;
                    try
                    {
                        label = drive.VolumeLabel;
                    }
                    catch
                    {
                    }

                    reply.Entries.Add(new NpBrowseEntry
                    {
                        Name = label.Length > 0 ? $"{drive.Name} ({label})" : drive.Name,
                        FullPath = drive.Name,
                        IsDirectory = true
                    });
                }
            }
            else
            {
                ListFolder(reply, query, Path.DirectorySeparatorChar.ToString());
            }
        }

        private static void ListFolder(QueryBrowseReply reply, QueryBrowse query, string path)
        {
            reply.Path = path;

            var parent = string.Equals(Path.GetPathRoot(path), path, StringComparison.Ordinal)
                ? null
                : Directory.GetParent(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;

            //Above the root of a drive are the drives. Above the root on Linux there is nothing.
            reply.Parent = parent ?? (OperatingSystem.IsWindows() ? string.Empty : null);

            var extensions = query.Extensions
                .Where(o => string.IsNullOrWhiteSpace(o) == false)
                .Select(o => o.Trim().StartsWith('.') ? o.Trim() : "." + o.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
            var directory = new DirectoryInfo(path);
            var entries = new List<NpBrowseEntry>();

            foreach (var info in directory.EnumerateFileSystemInfos("*", options))
            {
                var isDirectory = info.Attributes.HasFlag(FileAttributes.Directory);

                if (isDirectory == false && (query.FoldersOnly || (extensions.Count > 0 && extensions.Contains(info.Extension) == false)))
                {
                    continue;
                }

                if (entries.Count >= MaximumEntries)
                {
                    reply.Truncated = true;
                    break;
                }

                entries.Add(new NpBrowseEntry
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = isDirectory,
                    Size = info is FileInfo file ? file.Length : 0,
                    ModifiedUtc = info.LastWriteTimeUtc
                });
            }

            reply.Entries.AddRange(entries
                .OrderByDescending(o => o.IsDirectory)
                .ThenBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase));
        }
    }
}
