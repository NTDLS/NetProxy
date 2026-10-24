using NetProxy.Client.Classes;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NTDLS.Localization;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// Picks a file or a folder on the machine that the service runs on, which is where the certificates, the plugins and the
    /// log are, even when this console runs on another machine.
    /// </summary>
    public partial class FormRemoteBrowser : Form
    {
        private readonly RmClient? _client;
        private readonly bool _foldersOnly;
        private readonly List<string> _extensions = new();
        private readonly string _initialPath = string.Empty;

        private string _currentPath = string.Empty;
        private string? _parent;
        private bool _loading;

        /// <summary>
        /// What was picked, as a path on the machine of the service. Valid when the dialog result is OK.
        /// </summary>
        public string SelectedPath { get; private set; } = string.Empty;

        public FormRemoteBrowser()
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
        }

        /// <param name="client">The connection to the service.</param>
        /// <param name="foldersOnly">Pick a folder (the one that is shown) instead of a file.</param>
        /// <param name="extensions">The extensions of the files to show (".pfx"), with a way to show all of them. Empty shows all files.</param>
        /// <param name="initialPath">Where to start: a folder, or a file whose folder is shown with it selected.</param>
        /// <param name="title">The title of the window.</param>
        public FormRemoteBrowser(RmClient client, bool foldersOnly, IEnumerable<string> extensions, string? initialPath, string title)
            : this()
        {
            _client = client;
            _foldersOnly = foldersOnly;
            _extensions.AddRange(extensions);
            _initialPath = initialPath?.Trim() ?? string.Empty;

            Text = title;

            checkBoxAllFiles.Visible = foldersOnly == false && _extensions.Count > 0;
            ColumnSize.Visible = foldersOnly == false;
            ColumnType.Visible = foldersOnly == false;
            buttonSelect.Text = Localize(foldersOnly ? "Select this folder" : "Select");
            buttonSelect.Width = foldersOnly ? 120 : 75;
            buttonSelect.Left = buttonCancel.Left - buttonSelect.Width - 6;
        }

        /// <summary>
        /// The headers of a grid are not translated by FormLocalizer.
        /// </summary>
        private void LocalizeGrids()
        {
            ColumnName.HeaderText = Localize("Name");
            ColumnType.HeaderText = Localize("Type");
            ColumnSize.HeaderText = Localize("Size");
            ColumnModified.HeaderText = Localize("Modified");
        }

        private async void FormRemoteBrowser_Shown(object? sender, EventArgs e)
        {
            if (_client != null)
            {
                await NavigateAsync(_initialPath);
            }
        }

        private async Task NavigateAsync(string path)
        {
            var client = _client;
            if (client == null || _loading)
            {
                return;
            }

            _loading = true;
            labelStatus.Text = Localize("Reading...");

            try
            {
                var query = new QueryBrowse
                {
                    Path = path,
                    FoldersOnly = _foldersOnly,
                    Extensions = _foldersOnly || checkBoxAllFiles.Checked ? new List<string>() : _extensions
                };

                var reply = await Task.Run(() => client.Query<QueryBrowseReply>(query));

                if (IsDisposed)
                {
                    return;
                }

                if (string.IsNullOrEmpty(reply.Message) == false)
                {
                    labelStatus.Text = reply.Message;
                    return;
                }

                ShowListing(reply);
            }
            catch (Exception ex)
            {
                if (IsDisposed == false)
                {
                    labelStatus.Text = ex.GetBaseException().Message;
                }
            }
            finally
            {
                _loading = false;
            }
        }

        private void ShowListing(QueryBrowseReply reply)
        {
            _currentPath = reply.Path;
            _parent = reply.Parent;
            textBoxPath.Text = reply.Path;
            buttonUp.Enabled = reply.Parent != null;

            dataGridViewEntries.Rows.Clear();

            foreach (var entry in reply.Entries)
            {
                var index = dataGridViewEntries.Rows.Add(
                    entry.Name,
                    entry.IsDirectory ? Localize("Folder") : Localize("File"),
                    entry.IsDirectory ? string.Empty : Formatting.Bytes((ulong)entry.Size),
                    entry.IsDirectory && reply.Path.Length == 0 ? string.Empty : entry.ModifiedUtc.ToLocalTime().ToString("g"));

                dataGridViewEntries.Rows[index].Tag = entry;
            }

            dataGridViewEntries.ClearSelection();
            dataGridViewEntries.CurrentCell = null;

            if (reply.Selected != null)
            {
                foreach (DataGridViewRow row in dataGridViewEntries.Rows)
                {
                    if (row.Tag is NpBrowseEntry { IsDirectory: false } entry && entry.Name == reply.Selected)
                    {
                        row.Selected = true;
                        dataGridViewEntries.CurrentCell = row.Cells[ColumnName.Index];
                        break;
                    }
                }
            }

            labelStatus.Text = reply.Truncated
                ? Localize("Only the first {0} items are shown.", reply.Entries.Count)
                : Localize("{0} items", reply.Entries.Count);

            UpdateSelectButton();
        }

        private NpBrowseEntry? SelectedEntry
            => dataGridViewEntries.SelectedRows.Count > 0 ? dataGridViewEntries.SelectedRows[0].Tag as NpBrowseEntry : null;

        private void UpdateSelectButton()
        {
            buttonSelect.Enabled = _foldersOnly
                ? _currentPath.Length > 0
                : SelectedEntry is { IsDirectory: false };
        }

        private void DataGridViewEntries_SelectionChanged(object? sender, EventArgs e) => UpdateSelectButton();

        private async void ButtonUp_Click(object? sender, EventArgs e)
        {
            if (_parent != null)
            {
                await NavigateAsync(_parent);
            }
        }

        private async void TextBoxPath_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await NavigateAsync(textBoxPath.Text);
            }
        }

        private async void CheckBoxAllFiles_CheckedChanged(object? sender, EventArgs e)
            => await NavigateAsync(_currentPath);

        private async void DataGridViewEntries_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dataGridViewEntries.Rows[e.RowIndex].Tag is not NpBrowseEntry entry)
            {
                return;
            }

            if (entry.IsDirectory)
            {
                await NavigateAsync(entry.FullPath);
            }
            else if (_foldersOnly == false)
            {
                Choose(entry.FullPath);
            }
        }

        private async void DataGridViewEntries_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back)
            {
                e.Handled = true;
                if (_parent != null)
                {
                    await NavigateAsync(_parent);
                }
            }
            else if (e.KeyCode == Keys.Enter && SelectedEntry is { } entry)
            {
                //Enter on a folder opens it, on a file it selects it.
                e.Handled = true;
                if (entry.IsDirectory)
                {
                    await NavigateAsync(entry.FullPath);
                }
                else if (_foldersOnly == false)
                {
                    Choose(entry.FullPath);
                }
            }
        }

        private void ButtonSelect_Click(object? sender, EventArgs e)
        {
            if (_foldersOnly)
            {
                if (_currentPath.Length > 0)
                {
                    Choose(_currentPath);
                }
            }
            else if (SelectedEntry is { IsDirectory: false } entry)
            {
                Choose(entry.FullPath);
            }
        }

        private void Choose(string path)
        {
            SelectedPath = path;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
