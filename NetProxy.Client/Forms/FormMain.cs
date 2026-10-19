using NetProxy.Client.Classes;
using NetProxy.Client.Properties;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Utilities;
using NTDLS.Helpers;
using NTDLS.Localization;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;
using NTDLS.WinFormsHelpers.Forms;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Client.Forms
{
    public partial class FormMain : Form
    {
        private ConnectionInfo? _connectionInfo = null;
        private RmClient? _messageClient = null;
        private readonly System.Windows.Forms.Timer _statsTimer = new() { Interval = 1000 };
        private readonly PerformanceHistory _history = new();
        private readonly PerformanceChart _performanceChart;
        private readonly UISettings _settings = UISettings.Load();
        private bool _timerTicking = false;
        private List<NpProxyGridStats> _lastStatistics = [];

        private readonly ToolStripStatusLabel _statusConnection = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripStatusLabel _statusProxies = new() { BorderSides = ToolStripStatusLabelBorderSides.Left };
        private readonly ToolStripStatusLabel _statusConnections = new() { BorderSides = ToolStripStatusLabelBorderSides.Left };
        private readonly ToolStripStatusLabel _statusTraffic = new() { BorderSides = ToolStripStatusLabelBorderSides.Left };

        public FormMain()
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            Theming.InitializeForm(this);

            statusStripMain.Items.AddRange([_statusConnection, _statusProxies, _statusConnections, _statusTraffic]);

            _performanceChart = new PerformanceChart(chartPerformance);

            ApplyLanguage();

            dataGridViewProxys.SelectionChanged += (_, _) => UpdateChart();
            dataGridViewProxys.KeyDown += DataGridViewProxys_KeyDown;

            _statsTimer.Tick += StatsTimer_Tick;
        }

        #region Language.

        private void LanguageToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var form = new FormLanguage(_settings.Language) { Icon = Icon };
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _settings.Language = form.SelectedLanguage;
            Localizer.Default.SetLanguage(_settings.Language);
            _settings.Save();
            ApplyLanguage();
        }

        /// <summary>
        /// Puts the text of the window in the current language. Called at the start and whenever the language changes.
        /// FormLocalizer translates what the designer put on the controls; the rest is set here.
        /// </summary>
        private void ApplyLanguage()
        {
            FormLocalizer.Apply(this);

            //The headers of a grid are not translated by FormLocalizer.
            ColumnStatus.HeaderText = Localize("Status");
            ColumnName.HeaderText = Localize("Name");
            ColumnProxyType.HeaderText = Localize("Type");
            ColummListenPort.HeaderText = Localize("Port");
            ColumnBytesTransferred.HeaderText = Localize("Bytes Transferred");
            ColumnTotalConnections.HeaderText = Localize("Total Conn.");
            ColumnCurrentConnections.HeaderText = Localize("Curr. Conn.");

            SetupToolTips();

            Text = _connectionInfo != null ? $"{Constants.FriendlyName} - {_connectionInfo.ServerName}" : Constants.FriendlyName;
            UpdateConnectionStatus();
            UpdateStatusBar();
            UpdateChart();
        }

        private void UpdateConnectionStatus()
        {
            _statusConnection.Text = _messageClient != null && _connectionInfo != null
                ? Localize("Connected to {0} as {1}", NpUtility.FormatHostPort(_connectionInfo.ServerName, _connectionInfo.Port), _connectionInfo.UserName)
                : Localize("Not connected");
        }

        #endregion

        private void SetupToolTips()
        {
            ColumnStatus.ToolTipText = Localize("A green check is a proxy that is running, a red cross one that is stopped.");
            ColumnName.ToolTipText = Localize("The name of the proxy.");
            ColumnProxyType.ToolTipText = Localize("The traffic type of the listener, and the IP versions it listens on.");
            ColummListenPort.ToolTipText = Localize("The port that the proxy listens on.");
            ColumnBytesTransferred.ToolTipText = Localize("Everything that the proxy has passed to and from its clients since the service started.");
            ColumnTotalConnections.ToolTipText = Localize("The connections that the proxy has accepted since the service started.");
            ColumnCurrentConnections.ToolTipText = Localize("The connections that are open now.");
        }

        private void FormMain_Shown(object? sender, EventArgs e)
        {
            if (ChangeConnection() == false)
            {
                Close();
            }
        }

        private async void StatsTimer_Tick(object? sender, EventArgs e)
        {
            var client = _messageClient;
            if (_timerTicking || client == null)
            {
                return;
            }

            _timerTicking = true;

            try
            {
                //The query waits on the service, so it must not be done on the thread that draws the window.
                var reply = await Task.Run(() => client.Query<QueryProxyStatisticsReply>(new QueryProxyStatistics()));

                if (ReferenceEquals(client, _messageClient) == false || IsDisposed)
                {
                    return;
                }

                ApplyStatistics(reply.Collection);

                //Another console may have added or deleted a proxy.
                var known = dataGridViewProxys.Rows.Cast<DataGridViewRow>().Select(o => o.Cells[ColumnId.Index].Value?.ToString()).ToHashSet();
                if (known.Count != reply.Collection.Count || reply.Collection.Any(o => known.Contains(o.Id.ToString()) == false))
                {
                    RefreshProxyList();
                }
            }
            catch
            {
                //The connection was lost. Losing it is reported by OnDisconnected.
            }
            finally
            {
                _timerTicking = false;
            }
        }

        private bool ChangeConnection()
        {
            _statsTimer.Stop();

            using var formConnect = new FormConnect();

            //The language can be chosen on the connect window, and this window follows it.
            formConnect.LanguageChanged += (_, _) =>
            {
                _settings.Language = UISettings.Load().Language;
                ApplyLanguage();
            };

            if (formConnect.ShowDialog() == DialogResult.OK)
            {
                if (_messageClient != null)
                {
                    _connectionLost = null;
                    _messageClient.Disconnect();
                }

                //The connect window made the connection: it is encrypted and logged in already.
                var connectionInfo = formConnect.GetConnectionInfo();
                var client = formConnect.TakeClient();

                _connectionInfo = connectionInfo;
                _messageClient = client;
                _history.Clear();
                _lastStatistics = [];

                _connectionLost = OnConnectionLost;
                client.OnNotificationReceived += MessageClient_OnNotificationReceived;
                client.OnDisconnected += MessageClient_OnDisconnected;

                Text = $"{Constants.FriendlyName} - {connectionInfo.ServerName}";
                UpdateConnectionStatus();

                RefreshProxyList();

                _statsTimer.Start();
                return true;
            }

            _statsTimer.Start();
            return false;
        }

        private void MessageClient_OnDisconnected(RmContext context)
        {
            if (_connectionLost != null)
            {
                if (Visible)
                {
                    if (InvokeRequired)
                    {
                        Invoke(_connectionLost);
                    }
                    else
                    {
                        _connectionLost();
                    }
                }
            }
        }

        private void MessageClient_OnNotificationReceived(RmContext context, IRmNotification payload)
        {
            if (payload is NotificationMessage message && IsDisposed == false)
            {
                Invoke(new Action(() => MessageBox.Show(message.Text, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk)));
            }
        }

        private void RefreshProxyList()
        {
            try
            {
                var result = _messageClient.EnsureNotNull().Query(new QueryProxyConfigurationList());
                PopulateProxyList(result?.Collection ?? []);
            }
            catch (Exception ex)
            {
                _statusConnection.Text = Localize("Could not read the proxy list: {0}", ex.GetBaseException().Message);
            }
        }

        #region Delegates.

        public delegate void ConnectionLost();

        private ConnectionLost? _connectionLost;
        public void OnConnectionLost()
        {
            try
            {
                _statsTimer.Stop();
                _messageClient = null;
                UpdateConnectionStatus();
                dataGridViewProxys.DataSource = null;
                if (Visible)
                {
                    if (ChangeConnection() == false)
                    {
                        Close();
                    }
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Proxy list and statistics.

        private void PopulateProxyList(List<NpProxyGridItem> proxies)
        {
            string? selectedProxyId = null;

            //Save the current selected row:
            if (dataGridViewProxys.SelectedRows.Count > 0)
            {
                selectedProxyId = dataGridViewProxys.SelectedRows[0].Cells[ColumnId.Index].Value?.ToString() ?? "";
            }

            dataGridViewProxys.AutoGenerateColumns = false;
            dataGridViewProxys.DataSource = proxies.OrderBy(o => o.Name).ToList();

            //Re-select the previously selected row. With none selected the chart shows all of the proxies together.
            dataGridViewProxys.ClearSelection();
            foreach (DataGridViewRow row in dataGridViewProxys.Rows)
            {
                if ((row.Cells[ColumnId.Index].Value?.ToString() ?? "") == selectedProxyId)
                {
                    dataGridViewProxys.CurrentCell = row.Cells[ColumnStatus.Index];
                    row.Selected = true;
                }
            }

            //The statistics are not part of the list, so show the ones that were last received until the next ones arrive.
            ApplyStatistics(_lastStatistics);
        }

        private void ApplyStatistics(List<NpProxyGridStats> stats)
        {
            _lastStatistics = stats;
            _history.Add(stats, DateTime.Now);

            foreach (DataGridViewRow row in dataGridViewProxys.Rows)
            {
                if (row.DataBoundItem is not NpProxyGridItem item)
                {
                    continue;
                }

                var stat = stats.FirstOrDefault(o => o.Id == item.Id);
                if (stat != null)
                {
                    row.Cells[ColumnBytesTransferred.Index].Value = Formatting.Bytes(stat.BytesWritten + stat.BytesRead);
                    row.Cells[ColumnTotalConnections.Index].Value = Formatting.Count(stat.TotalConnections);
                    row.Cells[ColumnCurrentConnections.Index].Value = Formatting.Count(stat.CurrentConnections);
                    item.IsRunning = stat.IsRunning;
                }

                row.Cells[ColumnStatus.Index].Value = item.IsRunning ? Resources.StateRunning : Resources.StateStopped;
            }

            UpdateStatusBar();
            UpdateChart();
        }

        private void UpdateStatusBar()
        {
            var latest = _history.Get(PerformanceHistory.All);
            var last = latest.Count > 0 ? latest[^1] : default;

            _statusProxies.Text = Localize("Proxies: {0} of {1} running", _lastStatistics.Count(o => o.IsRunning), _lastStatistics.Count);
            _statusConnections.Text = Localize("Connections: {0} ({1} total)",
                Formatting.Count(_lastStatistics.Sum(o => o.CurrentConnections)),
                Formatting.Count(_lastStatistics.Aggregate(0UL, (sum, o) => sum + o.TotalConnections)));
            _statusTraffic.Text = Localize("Sent {0} ({1})   Received {2} ({3})",
                Formatting.Rate(last.SentPerSecond), Formatting.Bytes(_lastStatistics.Aggregate(0UL, (sum, o) => sum + o.BytesWritten)),
                Formatting.Rate(last.ReceivedPerSecond), Formatting.Bytes(_lastStatistics.Aggregate(0UL, (sum, o) => sum + o.BytesRead)));
        }

        /// <summary>
        /// The chart shows the selected proxy, or all of them together when none is selected.
        /// </summary>
        private void UpdateChart()
        {
            var key = PerformanceHistory.All;
            var title = Localize("All proxies");

            if (dataGridViewProxys.SelectedRows.Count > 0 && dataGridViewProxys.SelectedRows[0].DataBoundItem is NpProxyGridItem selected)
            {
                key = selected.Id;
                title = selected.Name;
            }

            _performanceChart.Render(_history.Get(key), title, DateTime.Now);
        }

        #endregion

        #region Events.

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            Visible = false;
            _statsTimer.Stop();
            dataGridViewProxys.DataSource = null;
            _messageClient?.Disconnect();
        }

        private void DataGridViewProxys_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            var proxyId = Guid.Parse(dataGridViewProxys.Rows[e.RowIndex].Cells["ColumnId"]?.Value?.ToString() ?? "");

            using var formProxy = new FormProxy(_connectionInfo.EnsureNotNull(), proxyId);
            if (formProxy.ShowDialog() == DialogResult.OK)
            {
                RefreshProxyList();
            }
        }

        private void ConfigurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var form = new FormServerSettings(_connectionInfo.EnsureNotNull());
            form.ShowDialog();
        }

        /// <summary>
        /// The addresses that a proxy can be reached on, as URLs, for the Browse menu.
        /// </summary>
        private IEnumerable<string> GetBrowseUrls(NpProxyGridItem proxy)
        {
            var scheme = proxy.TrafficType == TrafficType.Https ? "https" : "http";
            var addresses = new List<string>();

            if (proxy.ListenOnAllAddresses)
            {
                //The proxy answers on every address of the machine that it runs on, in the IP version(s) it was set to.
                try
                {
                    foreach (var ipAddress in Dns.GetHostEntry(_connectionInfo.EnsureNotNull().ServerName).AddressList)
                    {
                        var wanted = proxy.BindingProtocol switch
                        {
                            BindingProtocol.IPv4 => ipAddress.AddressFamily == AddressFamily.InterNetwork,
                            BindingProtocol.IPv6 => ipAddress.AddressFamily == AddressFamily.InterNetworkV6,
                            _ => true
                        };

                        if (wanted)
                        {
                            addresses.Add(ipAddress.ToString());
                        }
                    }
                }
                catch
                {
                    //The name of the server could not be looked up, so there is nothing to browse to.
                }
            }
            else
            {
                addresses.AddRange(proxy.Bindings.Where(o => o.Enabled).Select(o => o.Address));
            }

            foreach (var address in addresses.Select(NpUtility.NormalizeHost).Distinct())
            {
                yield return $"{scheme}://{(address.Contains(':') ? $"[{address}]" : address)}:{proxy.ListenPort}/";
            }
        }

        /// <summary>
        /// Escape deselects the proxy, which shows the chart of all of the proxies again.
        /// </summary>
        private void DataGridViewProxys_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DeselectProxy();
                e.Handled = true;
            }
        }

        private void DeselectProxy()
        {
            dataGridViewProxys.ClearSelection();
            dataGridViewProxys.CurrentCell = null;
            UpdateChart();
        }

        private void DataGridViewProxys_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //Clicking empty space, or Ctrl-clicking the selected proxy, deselects it.
                var hit = dataGridViewProxys.HitTest(e.X, e.Y);
                if (hit.Type == DataGridViewHitTestType.None
                    || (hit.RowIndex >= 0 && ModifierKeys.HasFlag(Keys.Control) && dataGridViewProxys.Rows[hit.RowIndex].Selected))
                {
                    DeselectProxy();
                }

                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                NpProxyGridItem? proxy = null;
                var hti = dataGridViewProxys.HitTest(e.X, e.Y);
                if (hti.RowIndex >= 0)
                {
                    dataGridViewProxys.ClearSelection();
                    dataGridViewProxys.Rows[hti.RowIndex].Selected = true;
                    dataGridViewProxys.CurrentCell = dataGridViewProxys.Rows[hti.RowIndex].Cells[ColumnStatus.Index];
                    proxy = dataGridViewProxys.Rows[hti.RowIndex].DataBoundItem as NpProxyGridItem;
                }

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.ItemClicked += Menu_ItemClicked;
                UIUtility.AddMenuItem(menu, "Add", Localize("Add"));
                UIUtility.AddMenuItem(menu, "Refresh", Localize("Refresh"));
                menu.Items.Add("-");
                UIUtility.AddMenuItem(menu, "Start", Localize("Start")).Enabled = proxy != null && proxy.IsRunning == false;
                UIUtility.AddMenuItem(menu, "Stop", Localize("Stop")).Enabled = proxy != null && proxy.IsRunning == true;
                UIUtility.AddMenuItem(menu, "Edit", Localize("Edit")).Enabled = proxy != null;
                menu.Items.Add("-");
                UIUtility.AddMenuItem(menu, "Chart all proxies", Localize("Chart all proxies")).Enabled = dataGridViewProxys.SelectedRows.Count > 0;

                if (proxy != null && (proxy.TrafficType == TrafficType.Http || proxy.TrafficType == TrafficType.Https))
                {
                    var bindingMenu = new ToolStripMenuItem(Localize("Browse")) { Tag = "Browse" };

                    foreach (var url in GetBrowseUrls(proxy))
                    {
                        var menuItem = bindingMenu.DropDownItems.Add(url);
                        menuItem.Click += Browse_MenuItem_Click;
                        menuItem.Tag = url;
                    }

                    if (bindingMenu.DropDownItems.Count > 0)
                    {
                        menu.Items.Add("-");
                        menu.Items.Add(bindingMenu);
                    }
                }

                menu.Items.Add("-");
                UIUtility.AddMenuItem(menu, "Delete", Localize("Delete")).Enabled = proxy != null;
                menu.Show(dataGridViewProxys, e.X, e.Y);
            }
        }

        private void Browse_MenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem { Tag: string url })
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void Menu_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            Guid proxyId = Guid.Empty;

            if (dataGridViewProxys.CurrentRow != null
                && Guid.TryParse(dataGridViewProxys.CurrentRow.Cells[ColumnId.Index].Value?.ToString(), out var parsed))
            {
                proxyId = parsed;
            }

            //The tag is the key of the item, which does not depend on the language.
            var key = e.ClickedItem?.Tag as string;

            if (key != "Browse")
            {
                (sender as ContextMenuStrip)?.Hide();
            }

            switch (key)
            {
                case "Add":
                    using (FormProxy formProxy = new FormProxy(_connectionInfo.EnsureNotNull(), null))
                    {
                        if (formProxy.ShowDialog() == DialogResult.OK)
                        {
                            RefreshProxyList();
                        }
                    }
                    break;
                case "Chart all proxies":
                    dataGridViewProxys.ClearSelection();
                    UpdateChart();
                    break;
                case "Refresh":
                    RefreshProxyList();
                    break;
                case "Start":
                    _messageClient.EnsureNotNull().Notify(new NotificationStartProxy(proxyId));
                    RefreshProxyList();
                    break;
                case "Stop":
                    if (MessageBox.Show(Localize("Stop the selected proxy?"), Constants.FriendlyName, MessageBoxButtons.YesNo,
                            MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        _messageClient.EnsureNotNull().Notify(new NotificationStopProxy(proxyId));
                        RefreshProxyList();
                    }

                    break;
                case "Edit":
                    using (FormProxy formProxy = new FormProxy(_connectionInfo.EnsureNotNull(), proxyId))
                    {
                        if (formProxy.ShowDialog() == DialogResult.OK)
                        {
                            RefreshProxyList();
                        }
                    }
                    break;
                case "Delete":
                    if (MessageBox.Show(Localize("Delete the selected proxy?"), Constants.FriendlyName,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        _messageClient.EnsureNotNull().Notify(new NotificationDeleteProxy(proxyId));
                        RefreshProxyList();
                    }
                    break;
            }
        }

        #endregion

        #region Menu.
        private void ChangeConnectionToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            ChangeConnection();
        }

        private void AboutToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var form = new FormAbout(Constants.FriendlyName, this.Icon, Resources.Logo128);
            form.ShowDialog();
        }

        private void ExitToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Close();
        }

        #endregion
    }
}
