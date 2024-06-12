using NetProxy.Client.Classes;
using NetProxy.Client.Properties;
using NetProxy.Library;
using NetProxy.Library.MessageHubPayloads.Notifications;
using NetProxy.Library.MessageHubPayloads.Queries;
using NetProxy.Library.Payloads;
using NetProxy.Library.Utilities;
using NTDLS.ReliableMessaging;
using System.Net;

namespace NetProxy.Client.Forms
{
    public partial class FormMain : Form
    {
        private ConnectionInfo? _connectionInfo = null;
        private RmClient? _messageClient = null;
        private readonly System.Windows.Forms.Timer? _statsTimer = null;

        public FormMain()
        {
            InitializeComponent();
            _populateProxyList = OnPopulateProxyList;
            _populateProxyListStats = OnPopulateProxyListStats;
            _connectionLost = OnConnectionLost;
            _sendMessage = OnSendMessage;

            _statsTimer = new();
            _statsTimer.Interval = 1000;
            _statsTimer.Tick += StatsTimer_Tick;
        }

        private void FormMain_Shown(object? sender, EventArgs e)
        {
            if (ChangeConnection() == false)
            {
                Close();
            }
        }

        bool timerTicking = false;
        private void StatsTimer_Tick(object? sender, EventArgs e)
        {
            if (timerTicking)
            {
                return;
            }
            timerTicking = true;

            try
            {
                _messageClient?.Query<QueryProxyStatisticsReply>(new QueryProxyStatistics()).ContinueWith((o) =>
                {
                    if (o.IsCompletedSuccessfully && o.Result?.Collection != null)
                    {
                        Invoke(_populateProxyListStats, o.Result.Collection);
                    }
                });
            }
            finally
            {
                timerTicking = false;
            }
        }

        private bool ChangeConnection()
        {
            _statsTimer?.Stop();

            using (var formConnect = new FormConnect())
            {
                if (formConnect.ShowDialog() == DialogResult.OK)
                {
                    if (_messageClient != null)
                    {
                        _connectionLost = null;
                        _messageClient.Disconnect();
                    }

                    _connectionInfo = formConnect.GetConnectionInfo();

                    _messageClient = MessageClientFactory.Create(_connectionInfo);
                    if (_messageClient != null)
                    {
                        _connectionLost = OnConnectionLost;
                        _messageClient.OnNotificationReceived += _messageClient_OnNotificationReceived;
                        _messageClient.OnDisconnected += _messageClient_OnDisconnected;

                        RefreshProxyList();

                        _statsTimer?.Start();
                        return true;
                    }
                }
            }

            _statsTimer?.Start();
            return false;
        }


        private void _messageClient_OnDisconnected(RmContext context)
        {
            if (_connectionLost != null)
            {
                Invoke(_connectionLost);
            }
        }

        private void _messageClient_OnNotificationReceived(RmContext context, IRmNotification payload)
        {
            if (payload is NotificationMessage message)
            {
                Invoke(_sendMessage, message.Text);
            }
        }

        private void RefreshProxyList()
        {
            NpUtility.EnsureNotNull(_messageClient);

            _messageClient.Query<QueryProxyConfigurationListReply>(new QueryProxyConfigurationList()).ContinueWith((o) =>
            {
                if (o.IsCompletedSuccessfully && o.Result?.Collection != null)
                {
                    Invoke(_populateProxyList, o.Result.Collection);
                }
            });

        }

        #region Delegates.

        public delegate void ConnectionLost();

        private ConnectionLost? _connectionLost;
        public void OnConnectionLost()
        {
            try
            {
                _statsTimer?.Stop();
                dataGridViewProxys.DataSource = null;
                if (ChangeConnection() == false)
                {
                    Close();
                }
            }
            catch
            {
            }
        }

        public delegate void SendMessage(string message);

        readonly SendMessage _sendMessage;
        public void OnSendMessage(string message)
        {
            MessageBox.Show(message, Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        public delegate void PopulateProxyList(List<NpProxyGridItem> proxies);

        readonly PopulateProxyList _populateProxyList;
        public void OnPopulateProxyList(List<NpProxyGridItem> proxies)
        {
            string? selectedProxyId = null;

            //Save the current selected row:
            if (dataGridViewProxys.CurrentRow != null)
            {
                selectedProxyId = dataGridViewProxys.CurrentRow.Cells[ColumnId.Index].Value?.ToString() ?? "";
            }

            dataGridViewProxys.AutoGenerateColumns = false;
            dataGridViewProxys.DataSource = proxies.OrderBy(o => o.Name).ToList();

            //Set the status icons and re-select the previously selected row.
            foreach (DataGridViewRow row in dataGridViewProxys.Rows)
            {
                string proxyId = row.Cells[ColumnId.Index].Value?.ToString() ?? "";

                if (proxyId == selectedProxyId)
                {
                    dataGridViewProxys.CurrentCell = row.Cells[ColumnStatus.Index];
                }

                if (((NpProxyGridItem)row.DataBoundItem).IsRunning)
                {
                    row.Cells[ColumnStatus.Index].Value = Resources.StateRunning;
                }
                else
                {
                    row.Cells[ColumnStatus.Index].Value = Resources.StateStopped;
                }
            }
        }

        double _lastBytesSent = -1;
        double _lastBytesRecv = -1;
        int chartPointCount = 0;
        const int maxChartPointCount = 50;

        public delegate void PopulateProxyListStats(List<NpProxyGridStats> stats);

        readonly PopulateProxyListStats _populateProxyListStats;
        public void OnPopulateProxyListStats(List<NpProxyGridStats> stats)
        {
            var sentSeries = chartPerformance.Series["KB/s Sent"];
            var recvSeries = chartPerformance.Series["KB/s Recv"];
            var connectionsSeries = chartPerformance.Series["Connections"];

            var bytesSent = stats.Sum(od => (od.BytesWritten / 1024.0));
            var bytesRecv = stats.Sum(od => (od.BytesRead / 1024.0));
            var currentConn = stats.Sum(od => od.CurrentConnections);

            if (_lastBytesSent >= 0 || _lastBytesRecv >= 0)
            {
                double value = 0;

                value = bytesSent - _lastBytesSent;
                sentSeries.Points.AddXY(chartPointCount, value > 0 ? value : 0);

                value = bytesRecv - _lastBytesRecv;
                recvSeries.Points.AddXY(chartPointCount, value > 0 ? value : 0);

                connectionsSeries.Points.AddXY(chartPointCount, currentConn);
            }

            _lastBytesSent = bytesSent;
            _lastBytesRecv = bytesRecv;

            if (chartPointCount > maxChartPointCount)
            {
                sentSeries.Points.RemoveAt(0);
                recvSeries.Points.RemoveAt(0);
                connectionsSeries.Points.RemoveAt(0);
            }
            chartPointCount++;

            chartPerformance.ChartAreas[0].AxisX.Minimum = chartPointCount - maxChartPointCount;
            chartPerformance.ChartAreas[0].AxisX.Maximum = chartPointCount;

            foreach (DataGridViewRow row in dataGridViewProxys.Rows)
            {
                string proxyId = row.Cells[ColumnId.Index].Value?.ToString() ?? "";

                var stat = (from o in stats where o.Id.ToString() == proxyId select o).FirstOrDefault();
                if (stat != null)
                {
                    row.Cells[ColumnBytesTransferred.Index].Value = NpFormatters.FormatFileSize(stat.BytesWritten + stat.BytesRead);
                    row.Cells[ColumnTotalConnections.Index].Value = NpFormatters.FormatNumeric(stat.TotalConnections);
                    row.Cells[ColumnCurrentConnections.Index].Value = NpFormatters.FormatNumeric(stat.CurrentConnections);
                    ((NpProxyGridItem)row.DataBoundItem).IsRunning = stat.IsRunning;
                }

                if (((NpProxyGridItem)row.DataBoundItem).IsRunning)
                {
                    row.Cells[ColumnStatus.Index].Value = Resources.StateRunning;
                }
                else
                {
                    row.Cells[ColumnStatus.Index].Value = Resources.StateStopped;
                }
            }
        }

        #endregion

        #region Events. 

        private void dataGridViewProxys_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            var proxyId = Guid.Parse(dataGridViewProxys.Rows[e.RowIndex].Cells["ColumnId"]?.Value?.ToString() ?? "");

            NpUtility.EnsureNotNull(_connectionInfo);
            using (var formProxy = new FormProxy(_connectionInfo, proxyId))
            {
                if (formProxy.ShowDialog() == DialogResult.OK)
                {
                    RefreshProxyList();
                }
            }
        }

        private void configurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            NpUtility.EnsureNotNull(_connectionInfo);
            using (var formServerSettings = new FormServerSettings(_connectionInfo))
            {
                formServerSettings.ShowDialog();
            }
        }

        private void dataGridViewProxys_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                NpProxyGridItem? proxy = null;
                var hti = dataGridViewProxys.HitTest(e.X, e.Y);
                if (hti.RowIndex >= 0)
                {
                    dataGridViewProxys.ClearSelection();
                    dataGridViewProxys.Rows[hti.RowIndex].Selected = true;
                    proxy = dataGridViewProxys.Rows[hti.RowIndex].DataBoundItem as NpProxyGridItem;
                }

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.ItemClicked += Menu_ItemClicked;
                menu.Items.Add("Add");
                menu.Items.Add("Refresh");
                menu.Items.Add("-");
                menu.Items.Add("Start").Enabled = proxy != null && proxy.IsRunning == false;
                menu.Items.Add("Stop").Enabled = proxy != null && proxy.IsRunning == true;
                menu.Items.Add("Edit");

                if (proxy != null && (proxy.TrafficType == TrafficType.Http || proxy.TrafficType == TrafficType.Https))
                {
                    ToolStripMenuItem bindingMenu = new ToolStripMenuItem("Browse");
                    if (proxy.ListenOnAllAddresses)
                    {
                        NpUtility.EnsureNotNull(_connectionInfo);
                        IPHostEntry iphostentry = Dns.GetHostEntry(_connectionInfo.ServerName);
                        foreach (IPAddress ipaddress in iphostentry.AddressList)
                        {
                            if (
                                (proxy.BindingProtocol == BindingProtocol.Pv4 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                || (proxy.BindingProtocol == BindingProtocol.Pv6 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                )
                            {
                                string? address = null;
                                if (proxy.BindingProtocol == BindingProtocol.Pv4)
                                {
                                    address = ipaddress.ToString();
                                }
                                else
                                {
                                    address = string.Format("[{0}]", ipaddress.ToString());
                                }

                                string url = string.Format("{0}://{1}:{2}/", (proxy.TrafficType == TrafficType.Http ? "HTTP" : "HTTPS"), address, proxy.ListenPort);
                                var menuItem = bindingMenu.DropDownItems.Add(url);
                                menuItem.Click += Browse_MenuItem_Click;
                                menuItem.Tag = url;
                            }
                        }
                    }
                    else
                    {
                        foreach (var binding in proxy.Bindings)
                        {
                            if (binding.Enabled == true)
                            {
                                string? address = null;
                                if (proxy.BindingProtocol == BindingProtocol.Pv4)
                                {
                                    address = binding.Address;
                                }
                                else
                                {
                                    address = string.Format("[{0}]", binding.Address);
                                }

                                string url = string.Format("{0}://{1}:{2}/", (proxy.TrafficType == TrafficType.Http ? "HTTP" : "HTTPS"), address, proxy.ListenPort);
                                var menuItem = bindingMenu.DropDownItems.Add(url);
                                menuItem.Click += Browse_MenuItem_Click;
                                menuItem.Tag = url;
                            }
                        }
                    }

                    if (bindingMenu.DropDownItems.Count > 0)
                    {
                        menu.Items.Add("-");
                        menu.Items.Add(bindingMenu);
                    }
                }

                menu.Items.Add("-");
                menu.Items.Add("Delete").Enabled = proxy != null;
                menu.Show(dataGridViewProxys, e.X, e.Y);
            }
        }

        private void Browse_MenuItem_Click(object? sender, EventArgs e)
        {
            var senderObject = (sender as ToolStripMenuItem);
            if (senderObject != null && senderObject.Tag != null)
            {
                System.Diagnostics.Process.Start(senderObject.Tag.ToString() ?? "");
            }
        }

        private void Menu_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            Guid proxyId = Guid.Empty;

            if (dataGridViewProxys.CurrentRow != null)
            {
                proxyId = Guid.Parse(dataGridViewProxys.CurrentRow.Cells[ColumnId.Index].Value?.ToString() ?? "");
            }

            if (e.ClickedItem?.Text != "Browse")
            {
                (sender as ContextMenuStrip)?.Hide();
            }

            switch (e.ClickedItem?.Text)
            {
                case "Add":
                    NpUtility.EnsureNotNull(_connectionInfo);
                    using (FormProxy formProxy = new FormProxy(_connectionInfo, null))
                    {
                        if (formProxy.ShowDialog() == DialogResult.OK)
                        {
                            RefreshProxyList();
                        }
                    }
                    break;
                case "Refresh":
                    RefreshProxyList();
                    break;
                case "Start":
                    NpUtility.EnsureNotNull(_messageClient);
                    NpUtility.EnsureNotNull(proxyId);
                    _messageClient.Notify(new NotificationStartProxy(proxyId));
                    RefreshProxyList();
                    break;
                case "Stop":
                    NpUtility.EnsureNotNull(_messageClient);
                    NpUtility.EnsureNotNull(proxyId);
                    if (MessageBox.Show(@"Stop the selected proxy?", Constants.TitleCaption, MessageBoxButtons.YesNo,
                            MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        _messageClient.Notify(new NotificationStopProxy(proxyId));
                        RefreshProxyList();
                    }

                    break;
                case "Edit":
                    NpUtility.EnsureNotNull(_connectionInfo);
                    NpUtility.EnsureNotNull(proxyId);
                    using (FormProxy formProxy = new FormProxy(_connectionInfo, (proxyId)))
                    {
                        if (formProxy.ShowDialog() == DialogResult.OK)
                        {
                            RefreshProxyList();
                        }
                    }
                    break;
                case "Delete":
                    NpUtility.EnsureNotNull(proxyId);
                    NpUtility.EnsureNotNull(_messageClient);
                    if (MessageBox.Show(@"Delete the selected proxy?", Constants.TitleCaption,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        _messageClient.Notify(new NotificationDeleteProxy(proxyId));
                        RefreshProxyList();
                    }
                    break;
            }
        }

        #endregion

        #region Menu.
        private void changeConnectionToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            ChangeConnection();
        }

        private void aboutToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using (var formAbout = new FormAbout())
            {
                formAbout.ShowDialog();
            }
        }

        private void exitToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Close();
        }

        #endregion
    }
}
