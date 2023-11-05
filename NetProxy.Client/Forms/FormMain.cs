using NetProxy.Client.Classes;
using NetProxy.Client.Properties;
using NetProxy.Hub;
using NetProxy.Hub.MessageFraming;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Utilities;
using Newtonsoft.Json;
using System.Net;

namespace NetProxy.Client.Forms
{
    public partial class FormMain : Form
    {
        private ConnectionInfo? _connectionInfo = null;
        private NpHubPacketeer? _packeteer = null;
        private readonly System.Windows.Forms.Timer? _statsTimer = null;

        public FormMain()
        {
            InitializeComponent();
            _populateRouteList = OnPopulateRouteList;
            _populateRouteListStats = OnPopulateRouteListStats;
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

        private void StatsTimer_Tick(object? sender, EventArgs e)
        {
            _packeteer?.SendAll(Constants.CommandLables.GuiRequestRouteStatsList);
        }

        private bool ChangeConnection()
        {
            _statsTimer?.Stop();

            using (var formConnect = new FormConnect())
            {
                if (formConnect.ShowDialog() == DialogResult.OK)
                {
                    if (_packeteer != null)
                    {
                        _connectionLost = null;
                        _packeteer.Disconnect();
                    }

                    _connectionInfo = formConnect.GetConnectionInfo();

                    _packeteer = LoginPacketeerFactory.GetNewPacketeer(_connectionInfo);
                    if (_packeteer != null)
                    {
                        _connectionLost = OnConnectionLost;
                        _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
                        _packeteer.OnPeerDisconnected += Packeteer_OnPeerDisconnected;

                        RefreshRouteList();

                        _statsTimer?.Start();
                        return true;
                    }
                }
            }

            _statsTimer?.Start();
            return false;
        }

        private void Packeteer_OnPeerDisconnected(NpHubPacketeer sender, NetProxy.Hub.Common.NpHubPeer peer)
        {
            if (_connectionLost != null)
            {
                Invoke(_connectionLost);
            }
        }

        private void Packeteer_OnMessageReceived(NpHubPacketeer sender, NetProxy.Hub.Common.NpHubPeer peer, NpFrame packet)
        {
            if (packet.Label == Constants.CommandLables.GuiRequestRouteList)
            {
                Invoke(_populateRouteList, JsonConvert.DeserializeObject<List<NpRouteGridItem>>(packet.Payload));
            }
            else if (packet.Label == Constants.CommandLables.GuiSendMessage)
            {
                Invoke(_sendMessage, packet.Payload);
            }
            if (packet.Label == Constants.CommandLables.GuiRequestRouteStatsList)
            {
                Invoke(_populateRouteListStats, JsonConvert.DeserializeObject<List<NpRouteGridStats>>(packet.Payload));
            }
        }

        private void RefreshRouteList()
        {
            NpUtility.EnsureNotNull(_packeteer);
            _packeteer.SendAll(Constants.CommandLables.GuiRequestRouteList);
        }

        #region Delegates.

        public delegate void ConnectionLost();

        private ConnectionLost? _connectionLost;
        public void OnConnectionLost()
        {
            try
            {
                _statsTimer?.Stop();
                dataGridViewRoutes.DataSource = null;
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

        public delegate void PopulateRouteList(List<NpRouteGridItem> routes);

        readonly PopulateRouteList _populateRouteList;
        public void OnPopulateRouteList(List<NpRouteGridItem> routes)
        {
            string? selectedRouteId = null;

            //Save the current selected row:
            if (dataGridViewRoutes.CurrentRow != null)
            {
                selectedRouteId = dataGridViewRoutes.CurrentRow.Cells[ColumnId.Index].Value?.ToString() ?? "";
            }

            dataGridViewRoutes.AutoGenerateColumns = false;
            dataGridViewRoutes.DataSource = routes.OrderBy(o => o.Name).ToList();

            //Set the status icons and re-select the previously selected row.
            foreach (DataGridViewRow row in dataGridViewRoutes.Rows)
            {
                string routeId = row.Cells[ColumnId.Index].Value?.ToString() ?? "";

                if (routeId == selectedRouteId)
                {
                    dataGridViewRoutes.CurrentCell = row.Cells[ColumnStatus.Index];
                }

                if (((NpRouteGridItem)row.DataBoundItem).IsRunning)
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

        public delegate void PopulateRouteListStats(List<NpRouteGridStats> stats);

        readonly PopulateRouteListStats _populateRouteListStats;
        public void OnPopulateRouteListStats(List<NpRouteGridStats> stats)
        {
            var sentSeries = chartPerformance.Series["MB/s Sent"];
            var recvSeries = chartPerformance.Series["MB/s Recv"];
            var connectionsSeries = chartPerformance.Series["Connections"];

            var bytesSent = stats.Sum(od => (od.BytesSent / 1024.0));
            var bytesRecv = stats.Sum(od => (od.BytesReceived / 1024.0));
            var currentConn = stats.Sum(od => od.CurrentConnections);

            if (_lastBytesSent >= 0)
            {
                double value = 0;

                value = bytesSent - _lastBytesSent;
                sentSeries.Points.Add(value > 0 ? value : 0);

                value = bytesRecv - _lastBytesRecv;
                recvSeries.Points.Add(value > 0 ? value : 0);

                value = currentConn;
                connectionsSeries.Points.Add(value > 0 ? value : 0);
            }

            _lastBytesSent = bytesSent;
            _lastBytesRecv = bytesRecv;

            if (sentSeries.Points.Count > 50)
            {
                sentSeries.Points.RemoveAt(0);
                recvSeries.Points.RemoveAt(0);
                connectionsSeries.Points.RemoveAt(0);
            }

            foreach (DataGridViewRow row in dataGridViewRoutes.Rows)
            {
                string routeId = row.Cells[ColumnId.Index].Value?.ToString() ?? "";

                var stat = (from o in stats where o.Id.ToString() == routeId select o).FirstOrDefault();
                if (stat != null)
                {
                    row.Cells[ColumnBytesTransferred.Index].Value = NpFormatters.FormatFileSize(stat.BytesSent + stat.BytesReceived);
                    row.Cells[ColumnTotalConnections.Index].Value = NpFormatters.FormatNumeric(stat.TotalConnections);
                    row.Cells[ColumnCurrentConnections.Index].Value = NpFormatters.FormatNumeric(stat.CurrentConnections);
                    ((NpRouteGridItem)row.DataBoundItem).IsRunning = stat.IsRunning;
                }

                if (((NpRouteGridItem)row.DataBoundItem).IsRunning)
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

        private void dataGridViewRoutes_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            string routeId = dataGridViewRoutes.Rows[e.RowIndex].Cells["ColumnId"]?.Value?.ToString() ?? "";

            NpUtility.EnsureNotNull(_connectionInfo);
            using (var formRoute = new FormRoute(_connectionInfo, routeId))
            {
                if (formRoute.ShowDialog() == DialogResult.OK)
                {
                    RefreshRouteList();
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

        private void dataGridViewRoutes_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                NpRouteGridItem? route = null;
                var hti = dataGridViewRoutes.HitTest(e.X, e.Y);
                if (hti.RowIndex >= 0)
                {
                    dataGridViewRoutes.ClearSelection();
                    dataGridViewRoutes.Rows[hti.RowIndex].Selected = true;
                    route = dataGridViewRoutes.Rows[hti.RowIndex].DataBoundItem as NpRouteGridItem;
                }

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.ItemClicked += Menu_ItemClicked;
                menu.Items.Add("Add");
                menu.Items.Add("Refresh");
                menu.Items.Add("-");
                menu.Items.Add("Start").Enabled = route != null && route.IsRunning == false;
                menu.Items.Add("Stop").Enabled = route != null && route.IsRunning == true;
                menu.Items.Add("Edit");

                if (route != null && (route.TrafficType == TrafficType.Http || route.TrafficType == TrafficType.Https))
                {
                    ToolStripMenuItem bindingMenu = new ToolStripMenuItem("Browse");
                    if (route.ListenOnAllAddresses)
                    {
                        NpUtility.EnsureNotNull(_connectionInfo);
                        IPHostEntry iphostentry = Dns.GetHostEntry(_connectionInfo.ServerName);
                        foreach (IPAddress ipaddress in iphostentry.AddressList)
                        {
                            if (
                                (route.BindingProtocal == BindingProtocal.Pv4 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                || (route.BindingProtocal == BindingProtocal.Pv6 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                )
                            {
                                string? address = null;
                                if (route.BindingProtocal == BindingProtocal.Pv4)
                                {
                                    address = ipaddress.ToString();
                                }
                                else
                                {
                                    address = string.Format("[{0}]", ipaddress.ToString());
                                }

                                string url = string.Format("{0}://{1}:{2}/", (route.TrafficType == TrafficType.Http ? "HTTP" : "HTTPS"), address, route.ListenPort);
                                var menuItem = bindingMenu.DropDownItems.Add(url);
                                menuItem.Click += Browse_MenuItem_Click;
                                menuItem.Tag = url;
                            }
                        }
                    }
                    else
                    {
                        foreach (var binding in route.Bindings)
                        {
                            if (binding.Enabled == true)
                            {
                                string? address = null;
                                if (route.BindingProtocal == BindingProtocal.Pv4)
                                {
                                    address = binding.Address;
                                }
                                else
                                {
                                    address = string.Format("[{0}]", binding.Address);
                                }

                                string url = string.Format("{0}://{1}:{2}/", (route.TrafficType == TrafficType.Http ? "HTTP" : "HTTPS"), address, route.ListenPort);
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
                menu.Items.Add("Delete").Enabled = route != null;
                menu.Show(dataGridViewRoutes, e.X, e.Y);
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
            string routeId = string.Empty;

            if (dataGridViewRoutes.CurrentRow != null)
            {
                routeId = dataGridViewRoutes.CurrentRow.Cells[ColumnId.Index].Value?.ToString() ?? "";
            }

            if (e.ClickedItem?.Text != "Browse")
            {
                (sender as ContextMenuStrip)?.Hide();
            }

            switch (e.ClickedItem?.Text)
            {
                case "Add":
                    NpUtility.EnsureNotNull(_connectionInfo);
                    using (FormRoute formRoute = new FormRoute(_connectionInfo, null))
                    {
                        if (formRoute.ShowDialog() == DialogResult.OK)
                        {
                            RefreshRouteList();
                        }
                    }
                    break;
                case "Refresh":
                    RefreshRouteList();
                    break;
                case "Start":
                    NpUtility.EnsureNotNull(_packeteer);
                    NpUtility.EnsureNotNull(routeId);
                    _packeteer.SendAll(Constants.CommandLables.GuiPersistStartRoute, routeId);
                    RefreshRouteList();
                    break;
                case "Stop":
                    NpUtility.EnsureNotNull(_packeteer);
                    NpUtility.EnsureNotNull(routeId);
                    if (MessageBox.Show(@"Stop the selected route?", Constants.TitleCaption, MessageBoxButtons.YesNo,
                            MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        _packeteer.SendAll(Constants.CommandLables.GuiPersistStopRoute, routeId);
                        RefreshRouteList();
                    }

                    break;
                case "Edit":
                    NpUtility.EnsureNotNull(_connectionInfo);
                    NpUtility.EnsureNotNull(routeId);
                    using (FormRoute formRoute = new FormRoute(_connectionInfo, routeId))
                    {
                        if (formRoute.ShowDialog() == DialogResult.OK)
                        {
                            RefreshRouteList();
                        }
                    }
                    break;
                case "Delete":
                    NpUtility.EnsureNotNull(routeId);
                    NpUtility.EnsureNotNull(_packeteer);
                    if (MessageBox.Show(@"Delete the selected route?", Constants.TitleCaption,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        _packeteer.SendAll(Constants.CommandLables.GuiPersistDeleteRoute, routeId);
                        RefreshRouteList();
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
