using NetProxy.Client.Classes;
using NetProxy.Hub;
using NetProxy.Hub.MessageFraming;
using NetProxy.Library;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using Newtonsoft.Json;

namespace NetProxy.Client.Forms
{
    public partial class FormProxy : Form
    {
        private NpHubPacketeer? _packeteer = null;
        private string? _proxyId = null;
        private delegate void PopulateProxyInformation(NpProxyConfiguration proxy);
        private PopulateProxyInformation? _populateProxyInformation;

        public FormProxy()
        {
            InitializeComponent();
        }

        public FormProxy(ConnectionInfo connectionInfo, string? proxyId)
        {
            InitializeComponent();

            _populateProxyInformation = OnPopulateProxyInformation;

            _proxyId = proxyId ?? Guid.NewGuid().ToString();
            _packeteer = LoginPacketeerFactory.GetNewPacketeer(connectionInfo);
            if (_packeteer == null)
            {
                Close();
                return;
            }

            _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;

            var trafficTypes = new List<ComboItem>
            {
                new ComboItem("Raw", TrafficType.Raw),
                new ComboItem("HTTP", TrafficType.Http),
                new ComboItem("HTTPS", TrafficType.Https)
            };

            comboBoxTrafficType.DisplayMember = "Display";
            comboBoxTrafficType.ValueMember = "Value";
            comboBoxTrafficType.DataSource = trafficTypes;

            var bindingProtocol = new List<ComboItem>
            {
                new ComboItem("TCP/IP v4", BindingProtocal.Pv4),
                new ComboItem("TCP/IP v6", BindingProtocal.Pv6)
            };

            comboBoxBindingProtocol.DisplayMember = "Display";
            comboBoxBindingProtocol.ValueMember = "Value";
            comboBoxBindingProtocol.DataSource = bindingProtocol;

            var connectionPatterns = new List<ComboItem>
            {
                new ComboItem("Balanced", ConnectionPattern.Balanced),
                new ComboItem("Fail-Over", ConnectionPattern.FailOver),
                new ComboItem("Round-Robbin", ConnectionPattern.RoundRobbin)
            };

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
            textBoxStickySessionCacheExpiration.Text = Constants.DefaultStickySessionExpiration.ToString();
            checkBoxListenAutoStart.Checked = true;
            //----------------------------------------------------------------------------
        }

        private void FormProxy_Shown(object? sender, EventArgs e)
        {
            if (_proxyId != null)
            {
                NpUtility.EnsureNotNull(_packeteer);
                _packeteer.SendAll(Constants.CommandLables.GuiRequestProxy, _proxyId);
            }
        }

        private void OnPopulateProxyInformation(NpProxyConfiguration proxy)
        {
            textBoxDescription.Text = proxy.Description;
            textBoxProxyName.Text = proxy.Name;
            comboBoxTrafficType.SelectedValue = proxy.TrafficType;
            comboBoxBindingProtocol.SelectedValue = proxy.BindingProtocal;
            textBoxListenPort.Text = proxy.ListenPort.ToString();
            checkBoxListenOnAllAddresses.Checked = proxy.ListenOnAllAddresses;
            textBoxInitialBufferSize.Text = proxy.InitialBufferSize.ToString();
            textBoxMaxBufferSize.Text = proxy.MaxBufferSize.ToString();
            textBoxAcceptBacklogSize.Text = proxy.AcceptBacklogSize.ToString();
            comboBoxConnectionPattern.SelectedValue = proxy.Endpoints.ConnectionPattern;
            checkBoxListenAutoStart.Checked = proxy.AutoStart;
            checkBoxUseStickySessions.Checked = proxy.UseStickySessions;
            textBoxStickySessionCacheExpiration.Text = proxy.StickySessionCacheExpiration.ToString();

            foreach (var endpoint in proxy.Endpoints.Collection)
            {
                dataGridViewEndpoints.Rows.Add(
                    new string[] { endpoint.Enabled.ToString(), endpoint.Address, endpoint.Port.ToString(), endpoint.Description }
                );
            }

            foreach (var binding in proxy.Bindings)
            {
                dataGridViewBindings.Rows.Add(
                    new string[] { binding.Enabled.ToString(), binding.Address, binding.Description }
                );
            }

            foreach (var httpHeaderRule in proxy.HttpHeaderRules.Collection)
            {
                dataGridViewHTTPHeaders.Rows.Add(
                    new string[] { httpHeaderRule.Enabled.ToString(), httpHeaderRule.HeaderType.ToString(), httpHeaderRule.Verb.ToString(), httpHeaderRule.Name, httpHeaderRule.Action.ToString(), httpHeaderRule.Value }
                );
            }
        }

        private void Packeteer_OnMessageReceived(NpHubPacketeer sender, Hub.Common.NpHubPeer peer, NpFrame packet)
        {
            if (packet.Label == Constants.CommandLables.GuiRequestProxy)
            {
                NpUtility.EnsureNotNull(_populateProxyInformation);
                Invoke(_populateProxyInformation, JsonConvert.DeserializeObject<NpProxyConfiguration>(packet.Payload));
            }
        }

        private void buttonSave_Click(object? sender, EventArgs e)
        {
            var proxy = new NpProxyConfiguration();

            if (textBoxProxyName.Text.Trim().Length == 0)
            {
                MessageBox.Show("The proxy name is required.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (NpUtility.ValidateInt32(textBoxListenPort.Text, 1, 65535) == false)
            {
                MessageBox.Show("The listen port is required (between 1 and 65,535).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (NpUtility.ValidateInt32(textBoxInitialBufferSize.Text, 1024, 1073741824) == false)
            {
                MessageBox.Show("The initial buffer size is required (between 1024 and 1,073,741,824).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (NpUtility.ValidateInt32(textBoxMaxBufferSize.Text) == false)
            {
                MessageBox.Show("The max buffer size is required (between 1024 and 1,073,741,824).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (NpUtility.ValidateInt32(textBoxAcceptBacklogSize.Text, 0, 10000) == false)
            {
                MessageBox.Show("The accept backlog size is required (between 0 and 10,000).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (NpUtility.ValidateInt32(textBoxStickySessionCacheExpiration.Text, 1, 2592000) == false)
            {
                MessageBox.Show("The sticky session cach expiration (s) is required (between 1 and 2,592,000).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            NpUtility.EnsureNotNull(_proxyId);

            proxy.Id = Guid.Parse(_proxyId);
            proxy.Name = textBoxProxyName.Text;
            proxy.Description = textBoxDescription.Text;
            proxy.TrafficType = (TrafficType)Enum.Parse(typeof(TrafficType), comboBoxTrafficType.SelectedValue?.ToString() ?? "");
            proxy.BindingProtocal = (BindingProtocal)Enum.Parse(typeof(BindingProtocal), comboBoxBindingProtocol.SelectedValue?.ToString() ?? "");
            proxy.ListenPort = int.Parse(textBoxListenPort.Text);
            proxy.ListenOnAllAddresses = checkBoxListenOnAllAddresses.Checked;
            proxy.InitialBufferSize = int.Parse(textBoxInitialBufferSize.Text);
            proxy.MaxBufferSize = int.Parse(textBoxMaxBufferSize.Text);
            proxy.AcceptBacklogSize = int.Parse(textBoxAcceptBacklogSize.Text);
            proxy.Endpoints.ConnectionPattern = (ConnectionPattern)Enum.Parse(typeof(ConnectionPattern), comboBoxConnectionPattern.SelectedValue?.ToString() ?? "");
            proxy.AutoStart = checkBoxListenAutoStart.Checked;
            proxy.UseStickySessions = checkBoxUseStickySessions.Checked;
            proxy.StickySessionCacheExpiration = int.Parse(textBoxStickySessionCacheExpiration.Text);

            if (proxy.InitialBufferSize > proxy.MaxBufferSize)
            {
                MessageBox.Show("The max buffer size must be larger than the initial buffer size.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            foreach (DataGridViewRow row in dataGridViewEndpoints.Rows)
            {
                if (((string)(row.Cells[ColumnEndpointsAddress.Index].Value ?? "")) != string.Empty)
                {
                    proxy.Endpoints.Add(new NpEndpoint()
                    {
                        Enabled = bool.Parse(row.Cells[ColumnEndpointsEnabled.Index].Value?.ToString() ?? "True"),
                        Address = row.Cells[ColumnEndpointsAddress.Index].Value?.ToString() ?? "",
                        Port = int.Parse(row.Cells[ColumnEndpointsPort.Index].Value?.ToString() ?? "0"),
                        Description = row.Cells[ColumnEndpointsDescription.Index].Value?.ToString() ?? ""
                    });
                }
            }

            foreach (DataGridViewRow row in dataGridViewBindings.Rows)
            {
                if (((string)(row.Cells[ColumnBindingsIPAddress.Index].Value ?? "")) != string.Empty)
                {


                    proxy.Bindings.Add(new Library.Routing.NpBinding()
                    {
                        Enabled = bool.Parse((row.Cells[ColumnBindingsEnabled.Index].Value?.ToString()) ?? "True"),
                        Address = row.Cells[ColumnBindingsIPAddress.Index].Value?.ToString() ?? "",
                        Description = row.Cells[ColumnBindingsDescription.Index].Value?.ToString() ?? ""
                    });
                }
            }

            foreach (DataGridViewRow row in dataGridViewHTTPHeaders.Rows)
            {
                if (((string)(row.Cells[ColumnHTTPHeadersHeader.Index].Value ?? "")) != string.Empty)
                {
                    proxy.HttpHeaderRules.Add(new NpHttpHeaderRule
                    {
                        Enabled = bool.Parse(row.Cells[ColumnHTTPHeadersEnabled.Index].Value?.ToString() ?? "True"),
                        Action = (HttpHeaderAction)Enum.Parse(typeof(HttpHeaderAction), row.Cells[ColumnHTTPHeadersAction.Index].Value?.ToString() ?? ""),
                        Description = row.Cells[ColumnHTTPHeadersDescription.Index].Value?.ToString() ?? "",
                        HeaderType = (HttpHeaderType)Enum.Parse(typeof(HttpHeaderType), row.Cells[ColumnHTTPHeadersType.Index].Value?.ToString() ?? ""),
                        Name = row.Cells[ColumnHTTPHeadersHeader.Index].Value?.ToString() ?? "",
                        Value = row.Cells[ColumnHTTPHeadersValue.Index].Value?.ToString() ?? "",
                        Verb = (HttpVerb)Enum.Parse(typeof(HttpVerb), row.Cells[ColumnHTTPHeadersVerb.Index].Value?.ToString() ?? "")
                    });
                }
            }

            foreach (var httpHeaderRule in proxy.HttpHeaderRules.Collection)
            {
                dataGridViewHTTPHeaders.Rows.Add(
                    new string[] { httpHeaderRule.Enabled.ToString(), httpHeaderRule.HeaderType.ToString(), httpHeaderRule.Verb.ToString(), httpHeaderRule.Name, httpHeaderRule.Action.ToString(), httpHeaderRule.Value }
                );
            }

            if (proxy.Endpoints.Collection.Count == 0)
            {
                MessageBox.Show("At least one end-point is required required.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (proxy.Bindings.Count == 0 && proxy.ListenOnAllAddresses == false)
            {
                MessageBox.Show("At least one binding is required unless [listen on all addresses] is selected.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            NpUtility.EnsureNotNull(_packeteer);
            _packeteer.SendAll(Constants.CommandLables.GuiPersistUpsertProxy, JsonConvert.SerializeObject(proxy));

            Thread.Sleep(500);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void FormProxy_FormClosed(object? sender, FormClosedEventArgs e)
        {
            NpUtility.EnsureNotNull(_packeteer);
            _packeteer.Disconnect();
        }

        private void comboBoxTrafficType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            TrafficType trafficType;

            if (Enum.TryParse<TrafficType>(comboBoxTrafficType.Text, true, out trafficType))
            {
                dataGridViewHTTPHeaders.ReadOnly = trafficType != TrafficType.Http;
            }
            else
            {
                dataGridViewHTTPHeaders.ReadOnly = true;
            }
        }

        private void checkBoxListenOnAllAddresses_CheckedChanged(object? sender, EventArgs e)
        {
            dataGridViewBindings.ReadOnly = checkBoxListenOnAllAddresses.Checked;
        }

        private void dataGridViewHTTPHeaders_Click(object? sender, EventArgs e)
        {
            if (dataGridViewHTTPHeaders.ReadOnly)
            {
                MessageBox.Show("HTTP header rules cannot be added because the traffic type is not HTTP.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void dataGridViewBindings_Click(object? sender, EventArgs e)
        {
            if (dataGridViewBindings.ReadOnly)
            {
                MessageBox.Show("Bindings cannot be added because [listen on all addresses] is selected.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }
    }
}