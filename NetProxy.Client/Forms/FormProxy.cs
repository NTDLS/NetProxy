using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.NullExtensions;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    public partial class FormProxy : Form
    {
        private bool _isNewProxy = false;
        private RmClient? _messageClient = null;
        private Guid? _proxyId = null;
        private delegate void PopulateProxyInformation(NpProxyConfiguration proxy);
        private PopulateProxyInformation? _populateProxyInformation;

        public FormProxy()
        {
            InitializeComponent();
        }

        public FormProxy(ConnectionInfo connectionInfo, Guid? proxyId)
        {
            InitializeComponent();

            _isNewProxy = proxyId == null;

            _populateProxyInformation = OnPopulateProxyInformation;
            _proxyId = proxyId ?? Guid.NewGuid();

            _messageClient = MessageClientFactory.Create(connectionInfo);
            if (_messageClient == null)
            {
                Close();
                return;
            }

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
                new ComboItem("TCP/IP v4", BindingProtocol.Pv4),
                new ComboItem("TCP/IP v6", BindingProtocol.Pv6)
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
            comboBoxBindingProtocol.SelectedValue = BindingProtocol.Pv4;
            checkBoxListenOnAllAddresses.Checked = true;
            textBoxInitialBufferSize.Text = $"{Constants.DefaultInitialBufferSize}";
            textBoxMaxBufferSize.Text = $"{Constants.DefaultMaxBufferSize}";
            textBoxAcceptBacklogSize.Text = $"{Constants.DefaultAcceptBacklogSize}";
            comboBoxConnectionPattern.SelectedValue = ConnectionPattern.RoundRobbin;
            textBoxStickySessionCacheExpiration.Text = $"{Constants.DefaultStickySessionExpiration}";
            checkBoxListenAutoStart.Checked = true;
            //----------------------------------------------------------------------------
        }

        private void FormProxy_Shown(object? sender, EventArgs e)
        {
            if (_isNewProxy == false && _proxyId != null)
            {
                _messageClient.EnsureNotNull().Query(new QueryProxyConfiguration((Guid)_proxyId)).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result?.ProxyConfiguration != null)
                    {
                        Invoke(_populateProxyInformation.EnsureNotNull(), t.Result?.ProxyConfiguration);
                    }
                });
            }
        }

        private void OnPopulateProxyInformation(NpProxyConfiguration proxy)
        {
            textBoxDescription.Text = proxy.Description;
            textBoxProxyName.Text = proxy.Name;
            comboBoxTrafficType.SelectedValue = proxy.TrafficType;
            comboBoxBindingProtocol.SelectedValue = proxy.BindingProtocol;
            textBoxListenPort.Text = $"{proxy.ListenPort}";
            checkBoxListenOnAllAddresses.Checked = proxy.ListenOnAllAddresses;
            textBoxInitialBufferSize.Text = $"{proxy.InitialBufferSize}";
            textBoxMaxBufferSize.Text = $"{proxy.MaxBufferSize}";
            textBoxAcceptBacklogSize.Text = $"{proxy.AcceptBacklogSize}";
            comboBoxConnectionPattern.SelectedValue = proxy.Endpoints.ConnectionPattern;
            checkBoxListenAutoStart.Checked = proxy.AutoStart;
            checkBoxUseStickySessions.Checked = proxy.UseStickySessions;
            textBoxStickySessionCacheExpiration.Text = $"{proxy.StickySessionCacheExpiration}";

            foreach (var endpoint in proxy.Endpoints.Collection)
            {
                dataGridViewEndpoints.Rows.Add(
                    [$"{endpoint.Enabled}", endpoint.Address, $"{endpoint.Port}", endpoint.Description]
                );
            }

            foreach (var binding in proxy.Bindings)
            {
                dataGridViewBindings.Rows.Add(
                    [$"{binding.Enabled}", binding.Address, binding.Description]
                );
            }

            foreach (var httpHeaderRule in proxy.HttpHeaderRules.Collection)
            {
                dataGridViewHTTPHeaders.Rows.Add(
                    [ $"{httpHeaderRule.Enabled}", $"{httpHeaderRule.HeaderType}",
                    $"{httpHeaderRule.Verb}", httpHeaderRule.Name, $"{httpHeaderRule.Action}", httpHeaderRule.Value ]
                );
            }
        }

        private void buttonSave_Click(object? sender, EventArgs e)
        {
            var proxy = new NpProxyConfiguration();

            if (textBoxProxyName.Text.Trim().Length == 0)
            {
                MessageBox.Show("The proxy name is required.",
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            try
            {
                textBoxProxyName.GetAndValidateText("The proxy name is required.");

                textBoxProxyName.GetAndValidateNumeric(1, 65535,
                    "The listen port is required (between [min] and [max]).");

                textBoxListenPort.GetAndValidateNumeric(1, 65535,
                    "The listen port is required (between [min] and [max]).");

                textBoxInitialBufferSize.GetAndValidateNumeric(1024, 1073741824,
                    "The initial buffer size is required (between [min] and [max].");

                textBoxMaxBufferSize.GetAndValidateNumeric(1024, 1073741824,
                    "The max buffer size is required (between [min] and [max].");

                textBoxAcceptBacklogSize.GetAndValidateNumeric(0, 10000,
                    "The accept backlog size is required (between [min] and [max]).");

                textBoxStickySessionCacheExpiration.GetAndValidateNumeric(1, 2592000,
                    "The sticky session cache expiration (s) is required (between [min] and [max]).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            proxy.Id = _proxyId.EnsureNotNullOrEmpty();
            proxy.Name = textBoxProxyName.Text;
            proxy.Description = textBoxDescription.Text;
            proxy.TrafficType = (TrafficType)Enum.Parse(typeof(TrafficType), comboBoxTrafficType.SelectedValue?.ToString() ?? "");
            proxy.BindingProtocol = (BindingProtocol)Enum.Parse(typeof(BindingProtocol), comboBoxBindingProtocol.SelectedValue?.ToString() ?? "");
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
                MessageBox.Show("The max buffer size must be larger than the initial buffer size.",
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
                    proxy.Bindings.Add(new NpBinding()
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
                    [ $"{httpHeaderRule.Enabled}", $"{httpHeaderRule.HeaderType}",
                    $"{httpHeaderRule.Verb}", httpHeaderRule.Name, $"{httpHeaderRule.Action}", httpHeaderRule.Value ]
                );
            }

            if (proxy.Endpoints.Collection.Count == 0)
            {
                MessageBox.Show("At least one end-point is required required.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (proxy.Bindings.Count == 0 && proxy.ListenOnAllAddresses == false)
            {
                MessageBox.Show("At least one binding is required unless [listen on all addresses] is selected.",
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            _messageClient.EnsureNotNull().Notify(new NotificationUpsertProxy(proxy));

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
            _messageClient.EnsureNotNull().Disconnect();
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
                MessageBox.Show("HTTP header rules cannot be added because the traffic type is not HTTP.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void dataGridViewBindings_Click(object? sender, EventArgs e)
        {
            if (dataGridViewBindings.ReadOnly)
            {
                MessageBox.Show("Bindings cannot be added because [listen on all addresses] is selected.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void checkBoxUseStickySessions_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}