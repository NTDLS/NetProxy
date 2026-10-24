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
        private List<NpProxyGridItem> _proxies = [];
        private DataGridViewColumn _proxySortColumn = null!;
        private bool _proxySortAscending = true;

        /// <summary>
        /// The connection list shows the connections of this proxy, and the rate of each is worked out from the bytes of the previous sample.
        /// </summary>
        private Guid _connectionsProxyId = Guid.Empty;
        private readonly Dictionary<Guid, ConnectionSample> _connectionSamples = [];

        private record ConnectionSample(DateTime Time, ulong BytesIn, ulong BytesOut, double InPerSecond, double OutPerSecond);

        private readonly ToolStripStatusLabel _statusConnection = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripStatusLabel _statusProxies = new() { BorderSides = ToolStripStatusLabelBorderSides.Left };
        private readonly ToolStripStatusLabel _statusConnections = new() { BorderSides = ToolStripStatusLabelBorderSides.Left };
        private readonly ToolStripStatusLabel _statusTraffic = new() { BorderSides = ToolStripStatusLabelBorderSides.Left };

        public FormMain()
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            Theming.InitializeForm(this);

            //The proxies start in order of name and the connections in order of when they started.
            _proxySortColumn = ColumnName;
            _connectionSorter.Column = ColumnConnStarted.Index;

            statusStripMain.Items.AddRange([_statusConnection, _statusProxies, _statusConnections, _statusTraffic]);

            _performanceChart = new PerformanceChart(chartPerformance);

            ApplyLanguage();

            SetupConnectionList();

            dataGridViewProxys.SelectionChanged += (_, _) =>
            {
                UpdateChart();
                SelectedProxyChanged();
            };
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
            SetupConnectionColumnHeaders();
            UpdateProxySortGlyphs();

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

                await RefreshConnectionsAsync(client);
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
                _connectionsProxyId = Guid.Empty;
                _connectionSamples.Clear();
                dataGridViewConnections.Rows.Clear();

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
            _proxies = proxies;
            BindProxies();

            //The statistics are not part of the list, so show the ones that were last received until the next ones arrive.
            ShowStatistics();
        }

        /// <summary>
        /// Puts the proxies in the grid in the order that was chosen, keeping the selected one selected.
        /// </summary>
        private void BindProxies()
        {
            string? selectedProxyId = null;

            //Save the current selected row:
            if (dataGridViewProxys.SelectedRows.Count > 0)
            {
                selectedProxyId = dataGridViewProxys.SelectedRows[0].Cells[ColumnId.Index].Value?.ToString() ?? "";
            }

            dataGridViewProxys.AutoGenerateColumns = false;
            dataGridViewProxys.DataSource = SortProxies(_proxies);

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

            UpdateProxySortGlyphs();
        }

        /// <summary>
        /// What a proxy sorts by in the chosen column: the value itself (numbers) rather than the text that is shown.
        /// </summary>
        private object ProxySortKey(NpProxyGridItem proxy, NpProxyGridStats? stat)
        {
            if (_proxySortColumn == ColumnStatus)
            {
                return stat?.IsRunning ?? proxy.IsRunning;
            }
            if (_proxySortColumn == ColumnProxyType)
            {
                return proxy.ProxyType;
            }
            if (_proxySortColumn == ColummListenPort)
            {
                return proxy.ListenPort;
            }
            if (_proxySortColumn == ColumnBytesTransferred)
            {
                return stat == null ? 0UL : stat.BytesWritten + stat.BytesRead;
            }
            if (_proxySortColumn == ColumnTotalConnections)
            {
                return stat?.TotalConnections ?? 0UL;
            }
            if (_proxySortColumn == ColumnCurrentConnections)
            {
                return stat?.CurrentConnections ?? 0;
            }

            return proxy.Name;
        }

        private List<NpProxyGridItem> SortProxies(IEnumerable<NpProxyGridItem> proxies)
        {
            var stats = new Dictionary<Guid, NpProxyGridStats>();
            foreach (var stat in _lastStatistics)
            {
                stats[stat.Id] = stat;
            }

            var comparer = Comparer<object?>.Create(CompareKeys);
            var keyed = proxies.Select(o => (Proxy: o, Key: ProxySortKey(o, stats.GetValueOrDefault(o.Id))));
            var ordered = _proxySortAscending ? keyed.OrderBy(o => o.Key, comparer) : keyed.OrderByDescending(o => o.Key, comparer);

            //Proxies with equal values stay in the order of their names.
            return ordered.ThenBy(o => o.Proxy.Name, StringComparer.CurrentCultureIgnoreCase).Select(o => o.Proxy).ToList();
        }

        private void UpdateProxySortGlyphs()
        {
            foreach (DataGridViewColumn column in dataGridViewProxys.Columns)
            {
                column.HeaderCell.SortGlyphDirection = column != _proxySortColumn ? SortOrder.None
                    : _proxySortAscending ? SortOrder.Ascending : SortOrder.Descending;
            }
        }

        private void DataGridViewProxys_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            var column = dataGridViewProxys.Columns[e.ColumnIndex];

            if (column == _proxySortColumn)
            {
                _proxySortAscending = !_proxySortAscending;
            }
            else
            {
                _proxySortColumn = column;
                _proxySortAscending = true;
            }

            BindProxies();
            ShowStatistics();
        }

        private void ApplyStatistics(List<NpProxyGridStats> stats)
        {
            _lastStatistics = stats;
            _history.Add(stats, DateTime.Now);

            ShowStatistics();
        }

        private void ShowStatistics()
        {
            FillProxyStatistics();

            //The statistics change what the order is when it is by one of them: bind again only when the order is not the same any more.
            var wanted = SortProxies(_proxies).Select(o => (Guid?)o.Id);
            var shown = dataGridViewProxys.Rows.Cast<DataGridViewRow>().Select(o => (o.DataBoundItem as NpProxyGridItem)?.Id);
            if (wanted.SequenceEqual(shown) == false)
            {
                BindProxies();
                FillProxyStatistics();
            }

            UpdateStatusBar();
            UpdateChart();
        }

        private void FillProxyStatistics()
        {
            foreach (DataGridViewRow row in dataGridViewProxys.Rows)
            {
                if (row.DataBoundItem is not NpProxyGridItem item)
                {
                    continue;
                }

                var stat = _lastStatistics.FirstOrDefault(o => o.Id == item.Id);
                if (stat != null)
                {
                    row.Cells[ColumnBytesTransferred.Index].Value = Formatting.Bytes(stat.BytesWritten + stat.BytesRead);
                    row.Cells[ColumnTotalConnections.Index].Value = Formatting.Count(stat.TotalConnections);
                    row.Cells[ColumnCurrentConnections.Index].Value = Formatting.Count(stat.CurrentConnections);
                    item.IsRunning = stat.IsRunning;
                }

                row.Cells[ColumnStatus.Index].Value = item.IsRunning ? Resources.StateRunning : Resources.StateStopped;
            }
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

        #region Endpoint connections.

        /// <summary>
        /// The column titles in the current language. Each is a literal Localize() call so that the localization scan finds it.
        /// </summary>
        private static string[] GetConnectionColumnTitles() =>
        [
            Localize("Source IP"), Localize("Source Host"), Localize("Source Port"),
            Localize("Destination IP"), Localize("Destination Host"), Localize("Destination Port"),
            Localize("Protocol"), Localize("Version"), Localize("Encryption"),
            Localize("Bytes In"), Localize("Bytes Out"), Localize("In/s"), Localize("Out/s"),
            Localize("Started"), Localize("Duration"), Localize("Idle")
        ];

        private void SetupConnectionList()
        {
            //Avoids the flicker of updating the rows once a second.
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(dataGridViewConnections, true);
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(dataGridViewProxys, true);

            SetupConnectionColumnHeaders();
        }

        private void SetupConnectionColumnHeaders()
        {
            var titles = GetConnectionColumnTitles();

            for (int i = 0; i < titles.Length && i < dataGridViewConnections.Columns.Count; i++)
            {
                var column = dataGridViewConnections.Columns[i];
                column.HeaderText = titles[i];
                column.HeaderCell.SortGlyphDirection = i != _connectionSorter.Column ? SortOrder.None
                    : _connectionSorter.Ascending ? SortOrder.Ascending : SortOrder.Descending;
            }
        }

        /// <summary>
        /// What a row sorts by in each column: the value itself (numbers, times) rather than the text that is shown.
        /// </summary>
        private sealed class ConnectionRow(Guid id)
        {
            public Guid Id { get; } = id;
            public object[] Keys { get; set; } = [];
        }

        private sealed class ConnectionSorter : System.Collections.IComparer
        {
            public int Column { get; set; } = -1;
            public bool Ascending { get; set; } = true;

            public int Compare(object? x, object? y)
            {
                if (Column < 0 || (x as DataGridViewRow)?.Tag is not ConnectionRow a || (y as DataGridViewRow)?.Tag is not ConnectionRow b)
                {
                    return 0;
                }

                var result = CompareKeys(a.Keys[Column], b.Keys[Column]);
                if (result == 0)
                {
                    result = a.Id.CompareTo(b.Id); //Keeps rows with equal values from jumping around.
                }

                return Ascending ? result : -result;
            }
        }

        private readonly ConnectionSorter _connectionSorter = new();

        /// <summary>
        /// Text is compared without regard to case, everything else (numbers, times) by its value.
        /// </summary>
        private static int CompareKeys(object? a, object? b)
            => a is string left && b is string right
                ? string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase)
                : a is IComparable comparable ? comparable.CompareTo(b) : 0;

        /// <summary>
        /// Addresses sort by their value, not their text ("10.0.0.9" before "10.0.0.10").
        /// </summary>
        private static string IpSortKey(string address)
            => System.Net.IPAddress.TryParse(address, out var ip) ? Convert.ToHexString(ip.MapToIPv6().GetAddressBytes()) : address;

        /// <summary>
        /// Right-click on a connection: copy the cell that was clicked, or the whole row.
        /// </summary>
        private void DataGridViewConnections_CellMouseUp(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
            {
                return;
            }

            var row = dataGridViewConnections.Rows[e.RowIndex];
            dataGridViewConnections.CurrentCell = row.Cells[Math.Max(e.ColumnIndex, 0)];
            row.Selected = true;

            var cell = e.ColumnIndex >= 0 ? row.Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty : string.Empty;

            var menu = new ContextMenuStrip();
            menu.Closed += (_, _) => menu.BeginInvoke(menu.Dispose);

            var copyValue = menu.Items.Add(Localize("Copy value"), null, (_, _) => CopyToClipboard(cell));
            copyValue.Enabled = cell.Length > 0;
            menu.Items.Add(Localize("Copy row"), null,
                (_, _) => CopyToClipboard(string.Join('\t', row.Cells.Cast<DataGridViewCell>().Select(o => o.Value?.ToString() ?? string.Empty))));

            menu.Show(Cursor.Position);
        }

        private static void CopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                //The clipboard is in use by another program.
            }
        }

        private void DataGridViewConnections_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (_connectionSorter.Column == e.ColumnIndex)
            {
                _connectionSorter.Ascending = !_connectionSorter.Ascending;
            }
            else
            {
                _connectionSorter.Column = e.ColumnIndex;
                _connectionSorter.Ascending = true;
            }

            SetupConnectionColumnHeaders();
            dataGridViewConnections.Sort(_connectionSorter);
        }

        private Guid GetSelectedProxyId()
            => dataGridViewProxys.SelectedRows.Count > 0 && dataGridViewProxys.SelectedRows[0].DataBoundItem is NpProxyGridItem selected
                ? selected.Id : Guid.Empty;

        /// <summary>
        /// The connection list follows the selected proxy: clear it and fill it for the new one without waiting for the next tick.
        /// </summary>
        private void SelectedProxyChanged()
        {
            var proxyId = GetSelectedProxyId();
            if (proxyId == _connectionsProxyId)
            {
                return;
            }

            _connectionsProxyId = proxyId;
            _connectionSamples.Clear();
            dataGridViewConnections.Rows.Clear();

            var client = _messageClient;
            if (proxyId != Guid.Empty && client != null && _timerTicking == false)
            {
                _timerTicking = true;
                _ = RefreshConnectionsThenReleaseAsync(client);
            }
        }

        private async Task RefreshConnectionsThenReleaseAsync(RmClient client)
        {
            try
            {
                await RefreshConnectionsAsync(client);
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

        private async Task RefreshConnectionsAsync(RmClient client)
        {
            var proxyId = _connectionsProxyId;
            if (proxyId == Guid.Empty)
            {
                return;
            }

            var reply = await Task.Run(() => client.Query<QueryProxyConnectionsReply>(new QueryProxyConnections(proxyId)));

            //The selection or the connection may have changed while the service was answering.
            if (ReferenceEquals(client, _messageClient) == false || IsDisposed || proxyId != _connectionsProxyId)
            {
                return;
            }

            ApplyConnections(reply.Collection);
        }

        private void ApplyConnections(List<NpConnectionGridItem> connections)
        {
            var grid = dataGridViewConnections;
            var now = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            var present = new HashSet<Guid>();

            grid.SuspendLayout();
            try
            {
                var existing = grid.Rows.Cast<DataGridViewRow>().ToDictionary(o => ((ConnectionRow)o.Tag!).Id);

                foreach (var connection in connections)
                {
                    present.Add(connection.Id);

                    //The rates are the change since the previous sample. The first sample of a connection has no rate yet.
                    _connectionSamples.TryGetValue(connection.Id, out var previous);
                    var inPerSecond = 0.0;
                    var outPerSecond = 0.0;
                    if (previous != null)
                    {
                        var seconds = (now - previous.Time).TotalSeconds;
                        if (seconds > 0.1)
                        {
                            inPerSecond = (connection.BytesIn - previous.BytesIn) / seconds;
                            outPerSecond = (connection.BytesOut - previous.BytesOut) / seconds;
                        }
                        else
                        {
                            inPerSecond = previous.InPerSecond;
                            outPerSecond = previous.OutPerSecond;
                        }
                    }

                    if (previous == null || (now - previous.Time).TotalSeconds > 0.1)
                    {
                        _connectionSamples[connection.Id] = new ConnectionSample(now, connection.BytesIn, connection.BytesOut, inPerSecond, outPerSecond);
                    }

                    var encryption = (connection.ClientTls, connection.EndpointTls) switch
                    {
                        ("", "") => "",
                        (var c, "") => c,
                        ("", var e) => "-> " + e,
                        var (c, e) => c + " -> " + e
                    };

                    string[] cells =
                    [
                        connection.SourceAddress,
                        connection.SourceHostName ?? "",
                        connection.SourcePort.ToString(),
                        connection.DestinationAddress,
                        connection.DestinationHostName ?? "",
                        connection.IsConnectedToEndpoint ? connection.DestinationPort.ToString() : "",
                        connection.Protocol,
                        connection.Version,
                        encryption,
                        Formatting.Bytes(connection.BytesIn),
                        Formatting.Bytes(connection.BytesOut),
                        Formatting.Rate(inPerSecond),
                        Formatting.Rate(outPerSecond),
                        connection.StartDateTime.ToLocalTime().ToString("g"),
                        FormatDuration(utcNow - connection.StartDateTime),
                        FormatDuration(utcNow - connection.LastActivityDateTime)
                    ];

                    object[] keys =
                    [
                        IpSortKey(connection.SourceAddress), connection.SourceHostName ?? "", connection.SourcePort,
                        IpSortKey(connection.DestinationAddress), connection.DestinationHostName ?? "", connection.DestinationPort,
                        connection.Protocol, connection.Version, encryption,
                        connection.BytesIn, connection.BytesOut, inPerSecond, outPerSecond,
                        connection.StartDateTime, utcNow - connection.StartDateTime, utcNow - connection.LastActivityDateTime
                    ];

                    if (existing.TryGetValue(connection.Id, out var row))
                    {
                        ((ConnectionRow)row.Tag!).Keys = keys;

                        for (int i = 0; i < cells.Length; i++)
                        {
                            if (row.Cells[i].Value as string != cells[i])
                            {
                                row.Cells[i].Value = cells[i];
                            }
                        }
                    }
                    else
                    {
                        var index = grid.Rows.Add((object[])cells);
                        grid.Rows[index].Tag = new ConnectionRow(connection.Id) { Keys = keys };
                    }
                }

                foreach (var gone in existing.Where(o => present.Contains(o.Key) == false))
                {
                    grid.Rows.Remove(gone.Value);
                }

                foreach (var staleId in _connectionSamples.Keys.Where(o => present.Contains(o) == false).ToList())
                {
                    _connectionSamples.Remove(staleId);
                }

                grid.Sort(_connectionSorter);

                //The first row of a grid is selected when it gets its rows, and nothing was selected by the user.
                if (existing.Count == 0)
                {
                    grid.ClearSelection();
                    grid.CurrentCell = null;
                }
            }
            finally
            {
                grid.ResumeLayout();
            }
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero;
            }

            return span.TotalDays >= 1
                ? $"{(int)span.TotalDays}d {span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
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

        private void PluginsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var form = new FormPlugins(_connectionInfo.EnsureNotNull()) { Icon = Icon };
            form.ShowDialog(this);
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

            //A proxy that shares its port is reached by its host names (a wildcard is not a name to browse to).
            var defaultPort = (scheme == "https" && proxy.ListenPort == 443) || (scheme == "http" && proxy.ListenPort == 80);
            foreach (var host in proxy.HostNames.Where(o => o.Enabled && o.Host.StartsWith('*') == false))
            {
                yield return $"{scheme}://{host.Host}{(defaultPort ? "" : $":{proxy.ListenPort}")}/";
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
