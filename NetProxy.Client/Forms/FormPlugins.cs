using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Helpers;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// The plug-ins of the service: add, edit, delete and put them in the order that they are called in. A proxy opts in to the ones
    /// it wants on its Plugins tab.
    /// </summary>
    public class FormPlugins : Form
    {
        private readonly RmClient? _messageClient;
        private readonly List<string> _serviceMessages = new();
        private readonly DataGridView _grid = new();
        private List<NpPluginGridItem> _plugins = new();

        public FormPlugins(ConnectionInfo connectionInfo)
        {
            Text = Localize("Plugins");
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimumSize = new Size(720, 360);
            ClientSize = new Size(820, 420);

            _grid.Location = new Point(12, 12);
            _grid.Size = new Size(796, 330);
            _grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.ReadOnly = true;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = SystemColors.ButtonFace;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("Order", "#");
            _grid.Columns.Add("Name", Localize("Name"));
            _grid.Columns.Add("Enabled", Localize("Enabled"));
            _grid.Columns.Add("Status", Localize("Status"));
            _grid.Columns.Add("Plugin", Localize("Plugin"));
            _grid.Columns.Add("Version", Localize("Version"));
            _grid.Columns.Add("Assembly", Localize("Assembly"));
            _grid.Columns["Order"]!.FillWeight = 8;
            _grid.Columns["Enabled"]!.FillWeight = 14;
            _grid.Columns["Version"]!.FillWeight = 14;
            _grid.Columns["Assembly"]!.FillWeight = 60;
            _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) { Edit(); } };
            Controls.Add(_grid);

            var buttons = new (string Text, Action Click)[]
            {
                (Localize("Add..."), Add), (Localize("Edit..."), Edit), (Localize("Delete"), Delete),
                (Localize("Move up"), () => MoveSelected(-1)), (Localize("Move down"), () => MoveSelected(1)), (Localize("Reload"), Reload)
            };

            var x = 12;
            foreach (var (text, click) in buttons)
            {
                var button = new Button { Text = text, Location = new Point(x, 354), Size = new Size(95, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
                button.Click += (_, _) => click();
                Controls.Add(button);
                x += 101;
            }

            var close = new Button { Text = Localize("Close"), Location = new Point(713, 382), Size = new Size(95, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.OK };
            Controls.Add(close);
            CancelButton = close;

            Controls.Add(new Label
            {
                Text = Localize("Plugins are called in this order. A proxy uses the plugins that are ticked on its Plugins tab."),
                Location = new Point(12, 392),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            });

            Theming.InitializeForm(this);

            _messageClient = MessageClientFactory.Create(connectionInfo, out var error);
            if (_messageClient == null)
            {
                MessageBox.Show(error ?? Localize("Could not connect to the service."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                Load += (_, _) => Close();
                return;
            }

            _messageClient.OnNotificationReceived += (RmContext context, IRmNotification payload) =>
            {
                if (payload is NotificationMessage message)
                {
                    lock (_serviceMessages)
                    {
                        _serviceMessages.Add(message.Text);
                    }
                }
            };

            Shown += (_, _) => RefreshList();
            FormClosed += (_, _) => _messageClient?.Disconnect();
        }

        private NpPluginGridItem? Selected
            => _grid.CurrentRow != null && _grid.CurrentRow.Index < _plugins.Count ? _plugins[_grid.CurrentRow.Index] : null;

        private void RefreshList(Guid? select = null)
        {
            if (_messageClient == null)
            {
                return;
            }

            var reply = _messageClient.Query<QueryPluginListReply>(new QueryPluginList());
            _plugins = reply.Collection;

            _grid.Rows.Clear();
            for (var i = 0; i < _plugins.Count; i++)
            {
                var p = _plugins[i];
                var status = p.Enabled == false ? Localize("Disabled") : (p.IsLoaded ? Localize("Loaded") : $"{Localize("Not loaded")}: {p.LoadError}");
                var index = _grid.Rows.Add(i + 1, p.Name, p.Enabled ? Localize("Yes") : Localize("No"), status, p.PluginName, p.PluginVersion, p.AssemblyPath);

                _grid.Rows[index].Cells["Status"].ToolTipText = status;
                _grid.Rows[index].Cells["Plugin"].ToolTipText = p.PluginDescription;

                if (select == p.Id)
                {
                    _grid.CurrentCell = _grid.Rows[index].Cells["Name"];
                }
            }
        }

        /// <summary>
        /// The service changes things without answering, but it tells us when it could not do what was asked.
        /// </summary>
        private void SendAndRefresh(IRmNotification notification, Guid? select = null)
        {
            lock (_serviceMessages)
            {
                _serviceMessages.Clear();
            }

            _messageClient.EnsureNotNull().Notify(notification);
            Thread.Sleep(600);

            string? message;
            lock (_serviceMessages)
            {
                message = _serviceMessages.FirstOrDefault();
            }

            RefreshList(select);

            if (message != null)
            {
                MessageBox.Show(message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void Add() => EditAndSave(null);

        /// <summary>
        /// Shows the editor and saves what is entered. If the service does not accept it (for example the name is taken) it says why and
        /// the editor comes back with what was entered, so that nothing has to be typed again. Cancelling ends it.
        /// </summary>
        private void EditAndSave(NpPluginConfiguration? plugin)
        {
            while (true)
            {
                using var form = new FormPlugin(plugin);
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                QueryUpsertPluginReply reply;

                try
                {
                    reply = _messageClient.EnsureNotNull().Query<QueryUpsertPluginReply>(new QueryUpsertPlugin { Plugin = form.Result, MatchByName = false });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Localize("The plugin was not saved: {0}", ex.GetBaseException().Message), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    plugin = form.Result;
                    continue;
                }

                if (reply.Success == false)
                {
                    MessageBox.Show(Localize("The plugin was not saved. {0}", reply.Message ?? string.Empty), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    plugin = form.Result;
                    continue;
                }

                RefreshList(form.Result.Id);

                if (string.IsNullOrEmpty(reply.LoadError) == false)
                {
                    MessageBox.Show(Localize("The plugin was saved but could not be loaded: {0}", reply.LoadError), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }

                return;
            }
        }

        private void Edit()
        {
            if (Selected is not { } selected)
            {
                return;
            }

            //A copy, so that cancelling leaves the list as it was.
            EditAndSave(new NpPluginConfiguration
            {
                Id = selected.Id,
                Name = selected.Name,
                Description = selected.Description,
                Enabled = selected.Enabled,
                AssemblyPath = selected.AssemblyPath,
                TypeName = selected.TypeName,
                TimeoutMilliseconds = selected.TimeoutMilliseconds,
                Settings = new Dictionary<string, string>(selected.Settings)
            });
        }

        private void Delete()
        {
            if (Selected is not { } selected)
            {
                return;
            }

            if (MessageBox.Show(Localize("Delete the plugin '{0}'? Proxies stop using it.", selected.Name), Constants.FriendlyName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                SendAndRefresh(new NotificationDeletePlugin(selected.Id));
            }
        }

        private void MoveSelected(int direction)
        {
            if (Selected is { } selected)
            {
                SendAndRefresh(new NotificationMovePlugin(selected.Id, direction), selected.Id);
            }
        }

        private void Reload()
        {
            if (Selected is { } selected)
            {
                SendAndRefresh(new NotificationReloadPlugin(selected.Id), selected.Id);
            }
        }
    }
}
