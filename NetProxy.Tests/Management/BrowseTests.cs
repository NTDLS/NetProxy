using NetProxy.Tests.Support;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// The file picker of the console: it lists folders of the machine that the service runs on.
    /// </summary>
    [Collection(ManagementCollection.Name)]
    public class BrowseTests(ManagementFixture service) : ManagementTest(service)
    {
        private static TempDirectory Layout()
        {
            var directory = new TempDirectory();
            Directory.CreateDirectory(directory.File("sub"));
            Directory.CreateDirectory(directory.File("Another"));
            File.WriteAllText(directory.File("site.pfx"), "x");
            File.WriteAllText(directory.File("other.P12"), "xx");
            File.WriteAllText(directory.File("notes.txt"), "xxx");
            return directory;
        }

        [Fact]
        public void A_folder_lists_its_folders_first_and_then_its_files()
        {
            using var directory = Layout();

            var reply = Client.Browse(directory.Path);

            Assert.Null(reply.Message);
            Assert.Equal(directory.Path, reply.Path);
            Assert.Equal(["Another", "sub", "notes.txt", "other.P12", "site.pfx"], reply.Entries.Select(o => o.Name));
            Assert.All(reply.Entries.Take(2), o => Assert.True(o.IsDirectory));
            Assert.Equal(Path.GetDirectoryName(directory.Path), reply.Parent);
            Assert.Equal(3, reply.Entries.Single(o => o.Name == "notes.txt").Size);
            Assert.Equal(directory.File("sub"), reply.Entries.Single(o => o.Name == "sub").FullPath);
        }

        [Fact]
        public void The_extensions_choose_the_files_and_folders_are_always_listed()
        {
            using var directory = Layout();

            var reply = Client.Browse(directory.Path, false, ".pfx", "p12");

            Assert.Equal(["Another", "sub", "other.P12", "site.pfx"], reply.Entries.Select(o => o.Name));
        }

        [Fact]
        public void Folders_only_lists_no_files()
        {
            using var directory = Layout();

            var reply = Client.Browse(directory.Path, true);

            Assert.Equal(["Another", "sub"], reply.Entries.Select(o => o.Name));
        }

        [Fact]
        public void The_path_of_a_file_lists_its_folder_with_the_file_selected()
        {
            using var directory = Layout();

            var reply = Client.Browse(directory.File("site.pfx"));

            Assert.Equal(directory.Path, reply.Path);
            Assert.Equal("site.pfx", reply.Selected);
        }

        [Fact]
        public void A_path_that_does_not_exist_lists_the_nearest_folder_that_does()
        {
            using var directory = Layout();

            var reply = Client.Browse(Path.Combine(directory.Path, "sub", "missing", "deeper.log"));

            Assert.Null(reply.Message);
            Assert.Equal(directory.File("sub"), reply.Path);
            Assert.Null(reply.Selected);
        }

        [Fact]
        public void A_relative_path_is_relative_to_the_folder_of_the_service()
        {
            var reply = Client.Browse("Logs/none.log");

            Assert.Null(reply.Message);
            Assert.True(Path.IsPathRooted(reply.Path));
            Assert.StartsWith(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), reply.Path);
        }

        [Fact]
        public void An_empty_path_lists_the_drives_or_the_root()
        {
            var reply = Client.Browse("");

            Assert.Null(reply.Message);
            Assert.NotEmpty(reply.Entries);

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(string.Empty, reply.Path);
                Assert.All(reply.Entries, o => Assert.True(o.IsDirectory));
                Assert.Contains(reply.Entries, o => o.FullPath.Equals(Path.GetPathRoot(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                Assert.Equal("/", reply.Path);
            }
        }

        [Fact]
        public void The_top_of_a_drive_has_the_list_of_drives_above_it()
        {
            if (OperatingSystem.IsWindows() == false)
            {
                return;
            }

            var root = Path.GetPathRoot(AppContext.BaseDirectory)!;
            var reply = Client.Browse(root);

            Assert.Equal(root, reply.Path);
            Assert.Equal(string.Empty, reply.Parent);
        }
    }
}
