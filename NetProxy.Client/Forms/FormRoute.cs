using NetProxy.Client.Classes;
using NetProxy.Hub;
using NetProxy.Hub.MessageFraming;
using NetProxy.Library;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using Newtonsoft.Json;

namespace NetProxy.Client.Forms
{
    public partial class FormRoute : Form
    {
        private Packeteer? _packeteer = null;
        private string? _routeId = null;
        private delegate void PopulateRouteInformation(Route route);
        private PopulateRouteInformation? _populateRouteInformation;

        public FormRoute()
        {
            InitializeComponent();
        }

        public FormRoute(ConnectionInfo connectionInfo, string? routeId)
        {
            InitializeComponent();

            _populateRouteInformation = OnPopulateRouteInformation;

            _routeId = routeId ?? Guid.NewGuid().ToString();
            _packeteer = LoginPacketeerFactory.GetNewPacketeer(connectionInfo);
            if (_packeteer == null)
            {
                Close();
                return;
            }

            _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;

            var trafficTypes = new List<ComboItem>
            {
                new ComboItem("Binary", TrafficType.Binary),
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
                //connectionPatterns.Add(new ComboItem("Balanced", ConnectionPattern.Balanced)); //Not yet implemented.
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
            textBoxSpinLockCount.Text = Constants.DefaultSpinLockCount.ToString();
            checkBoxListenAutoStart.Checked = true;
            //----------------------------------------------------------------------------
        }

        private void FormRoute_Shown(object? sender, EventArgs e)
        {
            if (_routeId != null)
            {
                Utility.EnsureNotNull(_packeteer);
                _packeteer.SendAll(Constants.CommandLables.GuiRequestRoute, _routeId);
            }
        }

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
            checkBoxUseStickySessions.Checked = route.UseStickySessions;
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

        private void Packeteer_OnMessageReceived(Packeteer sender, Hub.Common.Peer peer, Frame packet)
        {
            if (packet.Label == Constants.CommandLables.GuiRequestRoute)
            {
                Utility.EnsureNotNull(_populateRouteInformation);
                Invoke(_populateRouteInformation, JsonConvert.DeserializeObject<Route>(packet.Payload));
            }
        }

        private void buttonSave_Click(object? sender, EventArgs e)
        {
            Route route = new Route();

            if (textBoxRouteName.Text.Trim().Length == 0)
            {
                MessageBox.Show("The route name is required.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Utility.ValidateInt32(textBoxListenPort.Text, 1, 65535) == false)
            {
                MessageBox.Show("The listen port is required (between 1 and 65,535).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Utility.ValidateInt32(textBoxInitialBufferSize.Text, 1024, 1073741824) == false)
            {
                MessageBox.Show("The initial buffer size is required (between 1024 and 1,073,741,824).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Utility.ValidateInt32(textBoxMaxBufferSize.Text) == false)
            {
                MessageBox.Show("The max buffer size is required (between 1024 and 1,073,741,824).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (Utility.ValidateInt32(textBoxAcceptBacklogSize.Text, 0, 10000) == false)
            {
                MessageBox.Show("The accept backlog size is required (between 0 and 10,000).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (Utility.ValidateInt32(textBoxStickySessionCacheExpiration.Text, 1, 2592000) == false)
            {
                MessageBox.Show("The sticky session cach expiration (s) is required (between 1 and 2,592,000).", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (Utility.ValidateInt32(textBoxSpinLockCount.Text, 1000, 100000000) == false)
            {
                MessageBox.Show("The spin-lock count is required (between 1000 and 100,000,000.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            Utility.EnsureNotNull(_routeId);

            route.Id = Guid.Parse(_routeId);
            route.Name = textBoxRouteName.Text;
            route.Description = textBoxDescription.Text;
            route.TrafficType = (TrafficType)Enum.Parse(typeof(TrafficType), comboBoxTrafficType.SelectedValue?.ToString() ?? "");
            route.BindingProtocal = (BindingProtocal)Enum.Parse(typeof(BindingProtocal), comboBoxBindingProtocol.SelectedValue?.ToString() ?? "");
            route.ListenPort = int.Parse(textBoxListenPort.Text);
            route.ListenOnAllAddresses = checkBoxListenOnAllAddresses.Checked;
            route.InitialBufferSize = int.Parse(textBoxInitialBufferSize.Text);
            route.MaxBufferSize = int.Parse(textBoxMaxBufferSize.Text);
            route.AcceptBacklogSize = int.Parse(textBoxAcceptBacklogSize.Text);
            route.Endpoints.ConnectionPattern = (ConnectionPattern)Enum.Parse(typeof(ConnectionPattern), comboBoxConnectionPattern.SelectedValue?.ToString() ?? "");
            route.AutoStart = checkBoxListenAutoStart.Checked;
            route.UseStickySessions = checkBoxUseStickySessions.Checked;
            route.StickySessionCacheExpiration = int.Parse(textBoxStickySessionCacheExpiration.Text);
            route.SpinLockCount = int.Parse(textBoxSpinLockCount.Text);

            if (route.InitialBufferSize > route.MaxBufferSize)
            {
                MessageBox.Show("The max buffer size must be larger than the initial buffer size.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            foreach (DataGridViewRow row in dataGridViewEndpoints.Rows)
            {
                if (((string)(row.Cells[ColumnEndpointsAddress.Index].Value ?? "")) != string.Empty)
                {
                    route.Endpoints.Add(new Endpoint()
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


                    route.Bindings.Add(new Library.Routing.Binding()
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
                    route.HttpHeaderRules.Add(new HttpHeaderRule
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

            Utility.EnsureNotNull(_packeteer);
            _packeteer.SendAll(Constants.CommandLables.GuiPersistUpsertRoute, JsonConvert.SerializeObject(route));

            Thread.Sleep(500);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void FormRoute_FormClosed(object? sender, FormClosedEventArgs e)
        {
            Utility.EnsureNotNull(_packeteer);
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