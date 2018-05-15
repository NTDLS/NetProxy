using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NetProxy.Client.Classes;
using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.General;
using NetProxy.Library.Routing;
using NetProxy.Library.Utility;
using Newtonsoft.Json;

namespace NetProxy.Client.Forms
{
    public partial class FormRoute : Form
    {
        private Packeteer _packeteer = null;
        private string _routeId = null;

        public FormRoute()
        {
            InitializeComponent();
        }

        public FormRoute(ConnectionInfo connectionInfo, string routeId)
        {
            InitializeComponent();

            _populateRouteInformation = OnPopulateRouteInformation;

            this._routeId = routeId ?? Guid.NewGuid().ToString();
            _packeteer = LoginPacketeerFactory.GetNewPacketeer(connectionInfo);
            _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;

            var trafficTypes = new List<ComboItem>();
            trafficTypes.Add(new ComboItem("Binary", TrafficType.Binary));
            trafficTypes.Add(new ComboItem("HTTP", TrafficType.Http));
            trafficTypes.Add(new ComboItem("HTTPS", TrafficType.Https));

            comboBoxTrafficType.DisplayMember = "Display";
            comboBoxTrafficType.ValueMember = "Value";
            comboBoxTrafficType.DataSource = trafficTypes;

            var bindingProtocol = new List<ComboItem>();
            bindingProtocol.Add(new ComboItem("TCP/IP v4", BindingProtocal.Pv4));
            bindingProtocol.Add(new ComboItem("TCP/IP v6", BindingProtocal.Pv6));

            comboBoxBindingProtocol.DisplayMember = "Display";
            comboBoxBindingProtocol.ValueMember = "Value";
            comboBoxBindingProtocol.DataSource = bindingProtocol;

            var connectionPatterns = new List<ComboItem>();
            //connectionPatterns.Add(new ComboItem("Balanced", ConnectionPattern.Balanced)); //Not yet implemented.
            connectionPatterns.Add(new ComboItem("Fail-Over", ConnectionPattern.FailOver));
            connectionPatterns.Add(new ComboItem("Round-Robbin", ConnectionPattern.RoundRobbin));

            comboBoxConnectionPattern.DisplayMember = "Display";
            comboBoxConnectionPattern.ValueMember = "Value";
            comboBoxConnectionPattern.DataSource = connectionPatterns;

            ColumnBindingsEnabled.DefaultCellStyle.NullValue = true;
            ColumnEndpointsEnabled.DefaultCellStyle.NullValue = true;
            ColumnHTTPHeadersEnabled.DefaultCellStyle.NullValue = true;

            //----------------------------------------------------------------------------
            //Fill in some safe defaults:
            comboBoxTrafficType.SelectedValue = TrafficType.Http;
            comboBoxBindingProtocol.SelectedValue = BindingProtocal.Pv4;
            checkBoxListenOnAllAddresses.Checked = true;
            textBoxInitialBufferSize.Text = Constants.DefaultInitialBufferSize.ToString();
            textBoxMaxBufferSize.Text = Constants.DefaultMaxBufferSize.ToString();
            textBoxAcceptBacklogSize.Text = Constants.DefaultAcceptBacklogSize.ToString();
            comboBoxConnectionPattern.SelectedValue = ConnectionPattern.RoundRobbin;
            textBoxEncryptionInitTimeout.Text = Constants.DefaultEncryptionInitilizationTimeoutMs.ToString();
            textBoxStickySessionCacheExpiration.Text = Constants.DefaultStickySessionExpiration.ToString();
            textBoxSpinLockCount.Text = Constants.DefaultSpinLockCount.ToString();
            checkBoxListenAutoStart.Checked = true;
            //----------------------------------------------------------------------------
        }

        private void FormRoute_Shown(object sender, EventArgs e)
        {
            if (_routeId != null)
            {
                _packeteer.SendAll(Constants.CommandLables.GuiRequestRoute, _routeId);
            }
        }

        private delegate void PopulateRouteInformation(Route route);
        private PopulateRouteInformation _populateRouteInformation;
        private void OnPopulateRouteInformation(Route route)
        {
            textBoxDescription.Text = route.Description;
            textBoxRouteName.Text = route.Name;
            comboBoxTrafficType.SelectedValue = route.TrafficType;
            comboBoxBindingProtocol.SelectedValue = route.BindingProtocal;
            textBoxListenPort.Text = route.ListenPort.ToString();
            checkBoxListenOnAllAddresses.Checked = route.ListenOnAllAddresses;
            textBoxInitialBufferSize.Text = route.InitialBufferSize.ToString();
            textBoxMaxBufferSize.Text = route.MaxBufferSize.ToString();
            textBoxAcceptBacklogSize.Text = route.AcceptBacklogSize.ToString();
            comboBoxConnectionPattern.SelectedValue = route.Endpoints.ConnectionPattern;
            checkBoxListenAutoStart.Checked = route.AutoStart;
            checkBoxBindingIsTunnel.Checked = route.BindingIsTunnel;
            checkBoxEndpointIsTunnel.Checked = route.EndpointIsTunnel;
            checkBoxBindingIsTunnel.Checked = route.BindingIsTunnel;
            checkBoxTunnelBindingUseCompression.Checked = route.CompressBindingTunnel;
            checkBoxTunnelBindingUseEncryption.Checked = route.EncryptBindingTunnel;
            textBoxTunnelBindingPreSharedKey.Text = route.BindingPreSharedKey;
            checkBoxEndpointIsTunnel.Checked = route.EndpointIsTunnel;
            checkBoxTunnelEndpointUseCompression.Checked = route.CompressEndpointTunnel;
            checkBoxTunnelEndpointUseEncryption.Checked = route.EncryptEndpointTunnel;
            textBoxTunnelEndpointPreSharedKey.Text = route.EndpointPreSharedKey;
            checkBoxUseStickySessions.Checked = route.UseStickySessions;
            textBoxEncryptionInitTimeout.Text = route.EncryptionInitilizationTimeoutMs.ToString();
            textBoxStickySessionCacheExpiration.Text = route.StickySessionCacheExpiration.ToString();
            textBoxSpinLockCount.Text = route.SpinLockCount.ToString();

            foreach (var endpoint in route.Endpoints.List)
            {
                dataGridViewEndpoints.Rows.Add(
                    new string[] { endpoint.Enabled.ToString(), endpoint.Address, endpoint.Port.ToString(), endpoint.Description }
                );
            }

            foreach (var binding in route.Bindings)
            {
                dataGridViewBindings.Rows.Add(
                    new string[] { binding.Enabled.ToString(), binding.Address, binding.Description }
                );
            }

            foreach (var httpHeaderRule in route.HttpHeaderRules.List)
            {
                dataGridViewHTTPHeaders.Rows.Add(
                    new string[] { httpHeaderRule.Enabled.ToString(), httpHeaderRule.HeaderType.ToString(), httpHeaderRule.Verb.ToString(), httpHeaderRule.Name, httpHeaderRule.Action.ToString(), httpHeaderRule.Value }
                );
            }
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, NetProxy.Hub.Common.Peer peer, NetProxy.Hub.Common.Packet packet)
        {
            if (packet.Label == Constants.CommandLables.GuiRequestRoute)
            {
                this.Invoke(_populateRouteInformation, JsonConvert.DeserializeObject<Route>(packet.Payload));
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            Route route = new Route();

            if (textBoxRouteName.Text.Trim().Length == 0)
            {
                MessageBox.Show("The route name is required.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Strings.ValidateInt32(textBoxListenPort.Text, 1, 65535) == false)
            {
                MessageBox.Show("The listen port is required (between 1 and 65,535).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Strings.ValidateInt32(textBoxInitialBufferSize.Text, 1024, 1073741824) == false)
            {
                MessageBox.Show("The initial buffer size is required (between 1024 and 1,073,741,824).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Strings.ValidateInt32(textBoxMaxBufferSize.Text) == false)
            {
                MessageBox.Show("The max buffer size is required (between 1024 and 1,073,741,824).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Strings.ValidateInt32(textBoxAcceptBacklogSize.Text, 0, 10000) == false)
            {
                MessageBox.Show("The accept backlog size is required (between 0 and 10,000).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (Strings.ValidateInt32(textBoxEncryptionInitTimeout.Text, 1, 60000) == false)
            {
                MessageBox.Show("The encryption initilization timeout (ms) is required (between 1 and 60000).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (Strings.ValidateInt32(textBoxStickySessionCacheExpiration.Text, 1, 2592000) == false)
            {
                MessageBox.Show("The sticky session cach expiration (s) is required (between 1 and 2,592,000).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (Strings.ValidateInt32(textBoxSpinLockCount.Text, 1000, 100000000) == false)
            {
                MessageBox.Show("The spin-lock count is required (between 1000 and 100,000,000.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            route.Id = Guid.Parse(_routeId);
            route.Name = textBoxRouteName.Text;
            route.Description = textBoxDescription.Text;
            route.TrafficType = (TrafficType)Enum.Parse(typeof(TrafficType), comboBoxTrafficType.SelectedValue.ToString());
            route.BindingProtocal = (BindingProtocal)Enum.Parse(typeof(BindingProtocal), comboBoxBindingProtocol.SelectedValue.ToString());
            route.ListenPort = Int32.Parse(textBoxListenPort.Text);
            route.ListenOnAllAddresses = checkBoxListenOnAllAddresses.Checked;
            route.InitialBufferSize = Int32.Parse(textBoxInitialBufferSize.Text);
            route.MaxBufferSize = Int32.Parse(textBoxMaxBufferSize.Text);
            route.AcceptBacklogSize = Int32.Parse(textBoxAcceptBacklogSize.Text);
            route.Endpoints.ConnectionPattern = (ConnectionPattern)Enum.Parse(typeof(ConnectionPattern), comboBoxConnectionPattern.SelectedValue.ToString());
            route.AutoStart = checkBoxListenAutoStart.Checked;
            route.UseStickySessions = checkBoxUseStickySessions.Checked;

            route.EncryptionInitilizationTimeoutMs = Int32.Parse(textBoxEncryptionInitTimeout.Text);
            route.StickySessionCacheExpiration = Int32.Parse(textBoxStickySessionCacheExpiration.Text);
            route.SpinLockCount = Int32.Parse(textBoxSpinLockCount.Text);

            route.BindingIsTunnel = checkBoxBindingIsTunnel.Checked;
            route.CompressBindingTunnel = checkBoxTunnelBindingUseCompression.Checked;
            route.EncryptBindingTunnel = checkBoxTunnelBindingUseEncryption.Checked;
            route.BindingPreSharedKey = textBoxTunnelBindingPreSharedKey.Text;

            route.EndpointIsTunnel = checkBoxEndpointIsTunnel.Checked;
            route.CompressEndpointTunnel = checkBoxTunnelEndpointUseCompression.Checked;
            route.EncryptEndpointTunnel = checkBoxTunnelEndpointUseEncryption.Checked;
            route.EndpointPreSharedKey = textBoxTunnelEndpointPreSharedKey.Text;

            if (route.InitialBufferSize > route.MaxBufferSize)
            {
                MessageBox.Show("The max buffer size must be larger than the initial buffer size.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (route.BindingIsTunnel && route.EncryptBindingTunnel && route.BindingPreSharedKey.Trim().Length == 0)
            {
                MessageBox.Show("The Binding Tunnel Key cannot be blank when tunnel encryption is selected.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (route.EndpointIsTunnel && route.EncryptEndpointTunnel && route.EndpointPreSharedKey.Trim().Length == 0)
            {
                MessageBox.Show("The Endpoint Tunnel Key cannot be blank when tunnel encryption is selected.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            foreach (DataGridViewRow row in dataGridViewEndpoints.Rows)
            {
                if (((string)(row.Cells[ColumnEndpointsAddress.Index].Value ?? "")) != string.Empty)
                {
                    route.Endpoints.Add(new Endpoint()
                    {
                        Enabled = Boolean.Parse((row.Cells[ColumnEndpointsEnabled.Index].Value ?? "True").ToString()),
                        Address = (row.Cells[ColumnEndpointsAddress.Index].Value ?? "").ToString(),
                        Port = Int32.Parse((row.Cells[ColumnEndpointsPort.Index].Value ?? "0").ToString()),
                        Description = (row.Cells[ColumnEndpointsDescription.Index].Value ?? "").ToString()
                    });
                }
            }

            foreach (DataGridViewRow row in dataGridViewBindings.Rows)
            {
                if (((string)(row.Cells[ColumnBindingsIPAddress.Index].Value ?? "")) != string.Empty)
                {
                    route.Bindings.Add(new Library.Routing.Binding()
                    {
                        Enabled = Boolean.Parse((row.Cells[ColumnBindingsEnabled.Index].Value ?? "True").ToString()),
                        Address = (row.Cells[ColumnBindingsIPAddress.Index].Value ?? "").ToString(),
                        Description = (row.Cells[ColumnBindingsDescription.Index].Value ?? "").ToString()
                    });
                }
            }

            foreach (DataGridViewRow row in dataGridViewHTTPHeaders.Rows)
            {
                if (((string)(row.Cells[ColumnHTTPHeadersHeader.Index].Value ?? "")) != string.Empty)
                {
                    route.HttpHeaderRules.Add(new HttpHeaderRule
                    {
                        Enabled = Boolean.Parse((row.Cells[ColumnHTTPHeadersEnabled.Index].Value ?? "True").ToString()),
                        Action = (HttpHeaderAction)Enum.Parse(typeof(HttpHeaderAction), (row.Cells[ColumnHTTPHeadersAction.Index].Value ?? "").ToString()),
                        Description = (row.Cells[ColumnHTTPHeadersDescription.Index].Value ?? "").ToString(),
                        HeaderType = (HttpHeaderType)Enum.Parse(typeof(HttpHeaderType), (row.Cells[ColumnHTTPHeadersType.Index].Value ?? "").ToString()),
                        Name = (row.Cells[ColumnHTTPHeadersHeader.Index].Value ?? "").ToString(),
                        Value = (row.Cells[ColumnHTTPHeadersValue.Index].Value ?? "").ToString(),
                        Verb = (HttpVerb)Enum.Parse(typeof(HttpVerb), (row.Cells[ColumnHTTPHeadersVerb.Index].Value ?? "").ToString())
                    });
                }
            }

            foreach (var httpHeaderRule in route.HttpHeaderRules.List)
            {
                dataGridViewHTTPHeaders.Rows.Add(
                    new string[] { httpHeaderRule.Enabled.ToString(), httpHeaderRule.HeaderType.ToString(), httpHeaderRule.Verb.ToString(), httpHeaderRule.Name, httpHeaderRule.Action.ToString(), httpHeaderRule.Value }
                );
            }

            if (route.Endpoints.List.Count == 0)
            {
                MessageBox.Show("At least one end-point is required required.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (route.Bindings.Count == 0 && route.ListenOnAllAddresses == false)
            {
                MessageBox.Show("At least one binding is required unless [listen on all addresses] is selected.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            _packeteer.SendAll(Constants.CommandLables.GuiPersistUpsertRoute, JsonConvert.SerializeObject(route));

            System.Threading.Thread.Sleep(500);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void FormRoute_FormClosed(object sender, FormClosedEventArgs e)
        {
            _packeteer.Disconnect();
        }

        private void comboBoxTrafficType_SelectedIndexChanged(object sender, EventArgs e)
        {
            TrafficType trafficType;

            if (Enum.TryParse<TrafficType>(comboBoxTrafficType.Text, out trafficType))
            {
                dataGridViewHTTPHeaders.ReadOnly = trafficType != TrafficType.Http;
            }
            else
            {
                dataGridViewHTTPHeaders.ReadOnly = true;
            }
        }

        private void checkBoxListenOnAllAddresses_CheckedChanged(object sender, EventArgs e)
        {
            dataGridViewBindings.ReadOnly = checkBoxListenOnAllAddresses.Checked;
        }

        private void dataGridViewHTTPHeaders_Click(object sender, EventArgs e)
        {
            if (dataGridViewHTTPHeaders.ReadOnly)
            {
                MessageBox.Show("HTTP header rules cannot be added because the traffic type is not HTTP.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void dataGridViewBindings_Click(object sender, EventArgs e)
        {
            if (dataGridViewBindings.ReadOnly)
            {
                MessageBox.Show("Bindings cannot be added because [listen on all addresses] is selected.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void checkBoxBindingIsTunnel_CheckStateChanged(object sender, EventArgs e)
        {
            checkBoxTunnelBindingUseCompression.Enabled = checkBoxBindingIsTunnel.Checked;
            checkBoxTunnelBindingUseEncryption.Enabled = checkBoxBindingIsTunnel.Checked;
            textBoxTunnelBindingPreSharedKey.Enabled = checkBoxBindingIsTunnel.Checked;
        }

        private void checkBoxEndpointIsTunnel_CheckedChanged(object sender, EventArgs e)
        {
            checkBoxTunnelEndpointUseCompression.Enabled = checkBoxEndpointIsTunnel.Checked;
            checkBoxTunnelEndpointUseEncryption.Enabled = checkBoxEndpointIsTunnel.Checked;
            textBoxTunnelEndpointPreSharedKey.Enabled = checkBoxEndpointIsTunnel.Checked;
        }
    }
}