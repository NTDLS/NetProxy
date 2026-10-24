using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Helpers;
using NTDLS.Localization;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// The plug-ins of the service: add, edit, delete and put them in the order that they are called in. A proxy opts in to the ones
    /// it wants on its Plugins tab.
    /// </summary>
    public partial class FormPlugins : Form
    {
        private readonly RmClient? _messageClient;
        private List<NpPluginGridItem> _plugins = new();

        public FormPlugins()
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
        }

        public FormPlugins(ConnectionInfo connectionInfo)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);

            _messageClient = MessageClientFactory.Create(connectionInfo, out var error);
            if (_messageClient == null)
            {
                MessageBox.Show(error ?? Localize("Could not connect to the service."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                Load += (_, _) => Close();
                return;
            }

            Shown += (_, _) => RefreshList();
            FormClosed += (_, _) => _messageClient?.Disconnect();
        }

        /// <summary>
        /// The headers of a grid are not translated by FormLocalizer.
        /// </summary>
        private void LocalizeGrids()
        {
            ColumnName.HeaderText = Localize("Name");
            ColumnEnabled.HeaderText = Localize("Enabled");
            ColumnStatus.HeaderText = Localize("Status");
            ColumnPlugin.HeaderText = Localize("Plugin");
            ColumnVersion.HeaderText = Localize("Version");
            ColumnAssembly.HeaderText = Localize("Assembly");
        }

        private NpPluginGridItem? Selected
            => dataGridViewPlugins.CurrentRow != null && dataGridViewPlugins.CurrentRow.Index < _plugins.Count
                ? _plugins[dataGridViewPlugins.CurrentRow.Index] : null;

        private void RefreshList(Guid? select = null)
        {
            if (_messageClient == null)
            {
                return;
            }

            var reply = _messageClient.Query<QueryPluginListReply>(new QueryPluginList());
            _plugins = reply.Collection;

            dataGridViewPlugins.Rows.Clear();
            for (var i = 0; i < _plugins.Count; i++)
            {
                var p = _plugins[i];
                var status = p.Enabled == false ? Localize("Disabled") : (p.IsLoaded ? Localize("Loaded") : $"{Localize("Not loaded")}: {p.LoadError}");
                var index = dataGridViewPlugins.Rows.Add(i + 1, p.Name, p.Enabled ? Localize("Yes") : Localize("No"), status, p.PluginName, p.PluginVersion, p.AssemblyPath);

                dataGridViewPlugins.Rows[index].Cells[ColumnStatus.Index].ToolTipText = status;
                dataGridViewPlugins.Rows[index].Cells[ColumnPlugin.Index].ToolTipText = p.PluginDescription;

                if (select == p.Id)
                {
                    dataGridViewPlugins.CurrentCell = dataGridViewPlugins.Rows[index].Cells[ColumnName.Index];
                }
            }
        }

        private void DataGridViewPlugins_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                Edit();
            }
        }

        private void ButtonAdd_Click(object? sender, EventArgs e) => EditAndSave(null);

        private void ButtonEdit_Click(object? sender, EventArgs e) => Edit();

        private void ButtonMoveUp_Click(object? sender, EventArgs e) => Do(NpPluginAction.MoveUp);

        private void ButtonMoveDown_Click(object? sender, EventArgs e) => Do(NpPluginAction.MoveDown);

        private void ButtonReload_Click(object? sender, EventArgs e) => Do(NpPluginAction.Reload);

        /// <summary>
        /// Shows the editor and saves what is entered. If the service does not accept it (for example the name is taken) it says why and
        /// the editor comes back with what was entered, so that nothing has to be typed again. Cancelling ends it.
        /// </summary>
        private void EditAndSave(NpPluginConfiguration? plugin)
        {
            while (true)
            {
                using var form = new FormPlugin(plugin, _messageClient);
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

        private void ButtonDelete_Click(object? sender, EventArgs e)
        {
            if (Selected is not { } selected)
            {
                return;
            }

            if (MessageBox.Show(Localize("Delete the plugin '{0}'? Proxies stop using it.", selected.Name), Constants.FriendlyName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Do(NpPluginAction.Delete);
            }
        }

        /// <summary>
        /// Moves, reloads or deletes the selected plugin, and says if the service could not.
        /// </summary>
        private void Do(NpPluginAction action)
        {
            if (Selected is not { } selected)
            {
                return;
            }

            try
            {
                var reply = _messageClient.EnsureNotNull().Query<QueryActionReply>(new QueryPluginAction { Id = selected.Id, Action = action });

                RefreshList(action == NpPluginAction.Delete ? null : selected.Id);

                //A reload that could not load the plugin says why in its message.
                if (string.IsNullOrEmpty(reply.Message) == false)
                {
                    MessageBox.Show(reply.Message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetBaseException().Message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }
    }
}
