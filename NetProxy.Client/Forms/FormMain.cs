using NetProxy.Client.Classes;
using NetProxy.Client.Properties;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Hub;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NetProxy.Library.Utility;
using System.Windows.Forms.DataVisualization.Charting;
using System.Net;

namespace NetProxy.Client.Forms
{
    public partial class FormMain : Form
    {
        private List<RouteGridItem> routes = null;
        private ConnectionInfo connectionInfo = null;
        private Packeteer packeteer = null;
        private Timer statsTimer = null;

        public FormMain()
        {
            InitializeComponent();
            populateRouteList = OnPopulateRouteList;
            populateRouteListStats = OnPopulateRouteListStats;
            connectionLost = OnConnectionLost;
            sendMessage = OnSendMessage;

            statsTimer = new Timer();
            statsTimer.Interval = 1000;
            statsTimer.Tick += StatsTimer_Tick;
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            if (ChangeConnection() == false)
            {
                this.Close();
            }
        }

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            packeteer.SendAll(Constants.CommandLables.GUIRequestRouteStatsList);
        }

        private bool ChangeConnection()
        {
            statsTimer.Stop();

            using (var formConnect = new FormConnect())
            {
                if (formConnect.ShowDialog() == DialogResult.OK)
                {
                    if (packeteer != null)
                    {
                        packeteer.Disconnect();
                    }

                    connectionInfo = formConnect.GetConnectionInfo();

                    packeteer = LoginPacketeerFactory.GetNewPacketeer(connectionInfo);
                    packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
                    packeteer.OnPeerDisconnected += Packeteer_OnPeerDisconnected;

                    RefreshRouteList();

                    statsTimer.Start();
                    return true;
                }
            }

            statsTimer.Start();
            return false;
        }

        private void Packeteer_OnPeerDisconnected(Packeteer sender, NetProxy.Hub.Common.Peer peer)
        {
            this.Invoke(connectionLost);
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, NetProxy.Hub.Common.Peer peer, NetProxy.Hub.Common.Packet packet)
        {
            Console.WriteLine("{0}{1}", packet.Label, packet.Payload);

            if (packet.Label == Constants.CommandLables.GUIRequestRouteList)
            {
                this.Invoke(populateRouteList, JsonConvert.DeserializeObject<List<RouteGridItem>>(packet.Payload));
            }
            else if (packet.Label == Constants.CommandLables.GUISendMessage)
            {
                this.Invoke(sendMessage, packet.Payload);
            }
            if (packet.Label == Constants.CommandLables.GUIRequestRouteStatsList)
            {
                this.Invoke(populateRouteListStats, JsonConvert.DeserializeObject<List<RouteGridStats>>(packet.Payload));
            }
        }

        private void RefreshRouteList()
        {
            packeteer.SendAll(Constants.CommandLables.GUIRequestRouteList);
        }

        #region Delegates.

        public delegate void ConnectionLost();
        ConnectionLost connectionLost;
        public void OnConnectionLost()
        {
            try
            {
                statsTimer.Stop();
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
        SendMessage sendMessage;
        public void OnSendMessage(string message)
        {
            MessageBox.Show(message, Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        public delegate void PopulateRouteList(List<RouteGridItem> routes);
        PopulateRouteList populateRouteList;
        public void OnPopulateRouteList(List<RouteGridItem> routes)
        {
            string selectedRouteId = null;

            //Save the current selected row:
            if (dataGridViewRoutes.CurrentRow != null)
            {
                selectedRouteId = (dataGridViewRoutes.CurrentRow.Cells[ColumnId.Index].Value ?? "").ToString();
            }

            this.routes = routes;
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

        double lastBytesSent = -1;
        double lastBytesRecv = -1;

        public delegate void PopulateRouteListStats(List<RouteGridStats> stats);
        PopulateRouteListStats populateRouteListStats;
        public void OnPopulateRouteListStats(List<RouteGridStats> stats)
        {
            var sentSeries = chartPerformance.Series["MB/s Sent"];
            var recvSeries = chartPerformance.Series["MB/s Recv"];
            var connectionsSeries = chartPerformance.Series["Connections"];

            var bytesSent = stats.Sum(od => (od.BytesSent / 1024.0));
            var bytesRecv = stats.Sum(od => (od.BytesReceived / 1024.0));
            var currentConn = stats.Sum(od => od.CurrentConnections);

            if (lastBytesSent >= 0)
            {
                double value = 0;

                value = bytesSent - lastBytesSent;
                sentSeries.Points.Add(value > 0 ? value : 0);

                value = bytesRecv - lastBytesRecv;
                recvSeries.Points.Add(value > 0 ? value : 0);

                value = currentConn;
                connectionsSeries.Points.Add(value > 0 ? value : 0);
            }

            lastBytesSent = bytesSent;
            lastBytesRecv = bytesRecv;

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

        private void dataGridViewRoutes_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            string routeId = dataGridViewRoutes.Rows[e.RowIndex].Cells["ColumnId"].Value.ToString();

            using (FormRoute formRoute = new FormRoute(connectionInfo, routeId))
            {
                if (formRoute.ShowDialog() == DialogResult.OK)
                {
                    RefreshRouteList();
                }
            }
        }

        private void configurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var formServerSettings = new FormServerSettings(connectionInfo))
            {
                formServerSettings.ShowDialog();
            }
        }

        private void dataGridViewRoutes_MouseDown(object sender, MouseEventArgs e)
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

                if (route != null && (route.TrafficType == TrafficType.HTTP || route.TrafficType == TrafficType.HTTPS))
                {
                    ToolStripMenuItem bindingMenu = new ToolStripMenuItem("Browse");
                    if (route.ListenOnAllAddresses)
                    {
                        IPHostEntry iphostentry = Dns.GetHostEntry(connectionInfo.ServerName);
                        foreach (IPAddress ipaddress in iphostentry.AddressList)
                        {
                            if (
                                (route.BindingProtocal == BindingProtocal.IPv4 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                || (route.BindingProtocal == BindingProtocal.IPv6 && ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                )
                            {
                                string address = null;
                                if (route.BindingProtocal == BindingProtocal.IPv4)
                                {
                                    address = ipaddress.ToString();
                                }
                                else
                                {
                                    address = String.Format("[{0}]", ipaddress.ToString());
                                }

                                string url = String.Format("{0}://{1}:{2}/", (route.TrafficType == TrafficType.HTTP ? "HTTP" : "HTTPS"), address, route.ListenPort);
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
                                if (route.BindingProtocal == BindingProtocal.IPv4)
                                {
                                    address = binding.Address;
                                }
                                else
                                {
                                    address = String.Format("[{0}]", binding.Address);
                                }

                                string url = String.Format("{0}://{1}:{2}/", (route.TrafficType == TrafficType.HTTP ? "HTTP" : "HTTPS"), address, route.ListenPort);
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

        private void Browse_MenuItem_Click(object sender, EventArgs e)
        {
            var senderObject = (sender as ToolStripMenuItem);
            if (senderObject != null && senderObject.Tag != null)
            {
                System.Diagnostics.Process.Start(senderObject.Tag.ToString());
            }
        }

        private void Menu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
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
                    using (FormRoute formRoute = new FormRoute(connectionInfo, null))
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
                    packeteer.SendAll(Constants.CommandLables.GUIPersistStartRoute, routeId);
                    RefreshRouteList();
                    break;
                case "Stop":
                    packeteer.SendAll(Constants.CommandLables.GUIPersistStopRoute, routeId);
                    RefreshRouteList();
                    break;
                case "Delete":
                    if (MessageBox.Show("Delete the selected route?", Constants.TitleCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        packeteer.SendAll(Constants.CommandLables.GUIPersistDeleteRoute, routeId);
                        RefreshRouteList();
                    }
                    break;
            }
        }

        #endregion

        #region Menu.
        private void changeConnectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeConnection();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var formAbout = new FormAbout())
            {
                formAbout.ShowDialog();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        #endregion
    }
}
