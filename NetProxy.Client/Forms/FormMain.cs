using NetProxy.Client.Classes;
using NetProxy.Client.Properties;
using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Utility;
using Newtonsoft.Json;
using System.Net;

namespace NetProxy.Client.Forms
{
    public partial class FormMain : Form
    {
        private List<RouteGridItem> _routes = null;
        private ConnectionInfo _connectionInfo = null;
        private Packeteer _packeteer = null;
        private readonly System.Windows.Forms.Timer _statsTimer = null;

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
                this.Close();
            }
        }

        private void StatsTimer_Tick(object? sender, EventArgs e)
        {
            _packeteer.SendAll(Constants.CommandLables.GuiRequestRouteStatsList);
        }

        private bool ChangeConnection()
        {
            _statsTimer.Stop();

            using (var formConnect = new FormConnect())
            {
                if (formConnect.ShowDialog() == DialogResult.OK)
                {
                    if (_packeteer != null)
                    {
                        _packeteer.Disconnect();
                    }

                    _connectionInfo = formConnect.GetConnectionInfo();

                    _packeteer = LoginPacketeerFactory.GetNewPacketeer(_connectionInfo);
                    _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
                    _packeteer.OnPeerDisconnected += Packeteer_OnPeerDisconnected;

                    RefreshRouteList();

                    _statsTimer.Start();
                    return true;
                }
            }

            _statsTimer.Start();
            return false;
        }

        private void Packeteer_OnPeerDisconnected(Packeteer sender, NetProxy.Hub.Common.Peer peer)
        {
            this.Invoke(_connectionLost);
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, NetProxy.Hub.Common.Peer peer, NetProxy.Hub.Common.Packet packet)
        {
            Console.WriteLine("{0}{1}", packet.Label, packet.Payload);

            if (packet.Label == Constants.CommandLables.GuiRequestRouteList)
            {
                this.Invoke(_populateRouteList, JsonConvert.DeserializeObject<List<RouteGridItem>>(packet.Payload));
            }
            else if (packet.Label == Constants.CommandLables.GuiSendMessage)
            {
                this.Invoke(_sendMessage, packet.Payload);
            }
            if (packet.Label == Constants.CommandLables.GuiRequestRouteStatsList)
            {
                this.Invoke(_populateRouteListStats, JsonConvert.DeserializeObject<List<RouteGridStats>>(packet.Payload));
            }
        }

        private void RefreshRouteList()
        {
            _packeteer.SendAll(Constants.CommandLables.GuiRequestRouteList);
        }

        #region Delegates.

        public delegate void ConnectionLost();

        readonly ConnectionLost _connectionLost;
        public void OnConnectionLost()
        {
            try
            {
                _statsTimer.Stop();
                dataGridViewRoutes.DataSource = null;
                if (ChangeConnection() == false)
                {
                    this.Close();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public delegate void SendMessage(string message);

        readonly SendMessage _sendMessage;
        public void OnSendMessage(string message)
        {
            MessageBox.Show(message, Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        public delegate void PopulateRouteList(List<RouteGridItem> routes);

        readonly PopulateRouteList _populateRouteList;
        public void OnPopulateRouteList(List<RouteGridItem> routes)
        {
            string selectedRouteId = null;

            //Save the current selected row:
            if (dataGridViewRoutes.CurrentRow != null)
            {
                selectedRouteId = (dataGridViewRoutes.CurrentRow.Cells[ColumnId.Index].Value ?? "").ToString();
            }

            this._routes = routes;
            dataGridViewRoutes.AutoGenerateColumns = false;
            dataGridViewRoutes.DataSource = routes.OrderBy(o => o.Name).ToList();

            //Set the status icons and re-select the previously selected row.
            foreach (DataGridViewRow row in dataGridViewRoutes.Rows)
            {
                string routeId = (row.Cells[ColumnId.Index].Value ?? "").ToString();

                if (routeId == selectedRouteId)
                {
                    dataGridViewRoutes.CurrentCell = row.Cells[ColumnStatus.Index];
                }

                if (((RouteGridItem)row.DataBoundItem).IsRunning)
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

        public delegate void PopulateRouteListStats(List<RouteGridStats> stats);

        readonly PopulateRouteListStats _populateRouteListStats;
        public void OnPopulateRouteListStats(List<RouteGridStats> stats)
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
                string routeId = (row.Cells[ColumnId.Index].Value ?? "").ToString();

                var stat = (from o in stats where o.Id.ToString() == routeId select o).FirstOrDefault();
                if (stat != null)
                {
                    row.Cells[ColumnBytesTransferred.Index].Value = Formatters.FormatFileSize(stat.BytesSent + stat.BytesReceived);
                    row.Cells[ColumnTotalConnections.Index].Value = Formatters.FormatNumeric(stat.TotalConnections);
                    row.Cells[ColumnCurrentConnections.Index].Value = Formatters.FormatNumeric(stat.CurrentConnections);
                    ((RouteGridItem)row.DataBoundItem).IsRunning = stat.IsRunning;
                }

                if (((RouteGridItem)row.DataBoundItem).IsRunning)
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

            string routeId = dataGridViewRoutes.Rows[e.RowIndex].Cells["ColumnId"].Value.ToString();

            using (FormRoute formRoute = new FormRoute(_connectionInfo, routeId))
            {
                if (formRoute.ShowDialog() == DialogResult.OK)
                {
                    RefreshRouteList();
                }
            }
        }

        private void configurationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using (var formServerSettings = new FormServerSettings(_connectionInfo))
            {
                formServerSettings.ShowDialog();
            }
        }

        private void dataGridViewRoutes_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                RouteGridItem route = null;
                var hti = dataGridViewRoutes.HitTest(e.X, e.Y);
                if (hti.RowIndex >= 0)
                {
                    dataGridViewRoutes.ClearSelection();
                    dataGridViewRoutes.Rows[hti.RowIndex].Selected = true;
                    route = dataGridViewRoutes.Rows[hti.RowIndex].DataBoundItem as RouteGridItem;
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
                        IPHostEntry iphostentry = Dns.GetHostEntry(_connectionInfo.ServerName);
                        foreach (IPAddress ipaddress in iphostentry.AddressList)
                        {
                            if (
                                (route.BindingProtocal == BindingProtocal.Pv4 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                || (route.BindingProtocal == BindingProtocal.Pv6 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                )
                            {
                                string address = null;
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
                                string address = null;
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
                System.Diagnostics.Process.Start(senderObject.Tag.ToString());
            }
        }

        private void Menu_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            string routeId = string.Empty;

            if (dataGridViewRoutes.CurrentRow != null)
            {
                routeId = (dataGridViewRoutes.CurrentRow.Cells[ColumnId.Index].Value ?? "").ToString();
            }

            if (e.ClickedItem.Text != "Browse")
            {
                (sender as ContextMenuStrip)?.Hide();
            }

            switch (e.ClickedItem.Text)
            {
                case "Add":
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
                    _packeteer.SendAll(Constants.CommandLables.GuiPersistStartRoute, routeId);
                    RefreshRouteList();
                    break;
                case "Stop":
                    if (MessageBox.Show(@"Stop the selected route?", Constants.TitleCaption, MessageBoxButtons.YesNo,
                            MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        _packeteer.SendAll(Constants.CommandLables.GuiPersistStopRoute, routeId);
                        RefreshRouteList();
                    }

                    break;
                case "Edit":
                    using (FormRoute formRoute = new FormRoute(_connectionInfo, routeId))
                    {
                        if (formRoute.ShowDialog() == DialogResult.OK)
                        {
                            RefreshRouteList();
                        }
                    }
                    break;
                case "Delete":
                    if (MessageBox.Show(@"Delete the selected route?", Constants.TitleCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
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
            this.Close();
        }

        #endregion
    }
}
