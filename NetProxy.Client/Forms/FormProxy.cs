using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Helpers;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;
using System.Net;

namespace NetProxy.Client.Forms
{
    public partial class FormProxy : Form
    {
        private readonly bool _isNewProxy = false;
        private readonly RmClient? _messageClient = null;
        private readonly Guid? _proxyId = null;
        private readonly List<string> _serviceMessages = new();
        private delegate void PopulateProxyInformation(NpProxyConfiguration proxy);
        private PopulateProxyInformation? _populateProxyInformation;

        /// <summary>
        /// The traffic type of the listener before it was last changed, so that endpoints that followed it can follow it again.
        /// </summary>
        private TrafficType _previousTrafficType = TrafficType.Http;

        public FormProxy()
        {
            InitializeComponent();
            Theming.InitializeForm(this);
            GridTheming.Apply(this);
        }

        public FormProxy(ConnectionInfo connectionInfo, Guid? proxyId)
        {
            InitializeComponent();
            Theming.InitializeForm(this);
            GridTheming.Apply(this);

            _isNewProxy = proxyId == null;

            _populateProxyInformation = OnPopulateProxyInformation;
            _proxyId = proxyId ?? Guid.NewGuid();

            _messageClient = MessageClientFactory.Create(connectionInfo, out var error);
            if (_messageClient == null)
            {
                MessageBox.Show(error ?? "Could not connect to the service.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                Close();
                return;
            }

            //The service answers a change that it cannot carry out (such as a port that is in use) with a message.
            _messageClient.OnNotificationReceived += (RmContext context, IRmNotification payload) =>
            {
                if (payload is NotificationMessage message)
                {
                    lock (_serviceMessages)
                    {
                        _serviceMessages.Add(message.Text);
                    }
                }
            };

            var trafficTypes = new List<ComboItem>
            {
                new ("Raw", TrafficType.Raw),
                new ("HTTP", TrafficType.Http),
                new ("HTTPS", TrafficType.Https)
            };

            comboBoxTrafficType.DisplayMember = "Display";
            comboBoxTrafficType.ValueMember = "Value";
            comboBoxTrafficType.DataSource = trafficTypes;

            var bindingProtocol = new List<ComboItem>
            {
                new ("IPv4", BindingProtocol.IPv4),
                new ("IPv6", BindingProtocol.IPv6),
                new ("IPv4 and IPv6", BindingProtocol.Both)
            };

            comboBoxBindingProtocol.DisplayMember = "Display";
            comboBoxBindingProtocol.ValueMember = "Value";
            comboBoxBindingProtocol.DataSource = bindingProtocol;

            var connectionPatterns = new List<ComboItem>
            {
                new ("Balanced", ConnectionPattern.Balanced),
                new ("Fail-Over", ConnectionPattern.FailOver),
                new ("Round-Robbin", ConnectionPattern.RoundRobbin)
            };

            comboBoxConnectionPattern.DisplayMember = "Display";
            comboBoxConnectionPattern.ValueMember = "Value";
            comboBoxConnectionPattern.DataSource = connectionPatterns;

            ColumnBindingsEnabled.DefaultCellStyle.NullValue = true;
            ColumnEndpointsEnabled.DefaultCellStyle.NullValue = true;
            ColumnHTTPHeadersEnabled.DefaultCellStyle.NullValue = true;

            //A new endpoint starts with the traffic type of the listener, which is usually what it needs.
            dataGridViewEndpoints.DefaultValuesNeeded += DataGridViewEndpoints_DefaultValuesNeeded;
            dataGridViewEndpoints.DataError += (_, e) => e.ThrowException = false;
            dataGridViewHTTPHeaders.DataError += (_, e) => e.ThrowException = false;

            //----------------------------------------------------------------------------
            //Fill in some safe defaults:
            comboBoxTrafficType.SelectedValue = TrafficType.Http;
            comboBoxBindingProtocol.SelectedValue = BindingProtocol.Both;
            checkBoxListenOnAllAddresses.Checked = true;
            textBoxInitialBufferSize.Text = $"{Constants.DefaultInitialBufferSize}";
            textBoxMaxBufferSize.Text = $"{Constants.DefaultMaxBufferSize}";
            textBoxAcceptBacklogSize.Text = $"{Constants.DefaultAcceptBacklogSize}";
            comboBoxConnectionPattern.SelectedValue = ConnectionPattern.RoundRobbin;
            textBoxStickySessionCacheExpiration.Text = $"{Constants.DefaultStickySessionExpiration}";
            checkBoxListenAutoStart.Checked = true;
            //----------------------------------------------------------------------------

            _previousTrafficType = TrafficType.Http;
        }

        private void FormProxy_Shown(object? sender, EventArgs e)
        {
            if (_isNewProxy == false && _proxyId != null)
            {
                var result = _messageClient.EnsureNotNull().Query(new QueryProxyConfiguration((Guid)_proxyId));
                Invoke(_populateProxyInformation.EnsureNotNull(), result?.ProxyConfiguration);
            }

            UpdateTrafficTypeDependentControls();
        }

        private void OnPopulateProxyInformation(NpProxyConfiguration proxy)
        {
            textBoxDescription.Text = proxy.Description;
            textBoxProxyName.Text = proxy.Name;
            comboBoxTrafficType.SelectedValue = proxy.TrafficType;
            _previousTrafficType = proxy.TrafficType;
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
            textBoxCertificatePath.Text = proxy.TlsCertificatePath;
            textBoxCertificatePassword.Text = proxy.TlsCertificatePassword;

            foreach (var endpoint in proxy.Endpoints.Collection)
            {
                dataGridViewEndpoints.Rows.Add(
                    [$"{endpoint.Enabled}", endpoint.Address, $"{endpoint.Port}", $"{endpoint.TrafficType}", $"{endpoint.TlsIgnoreCertificateErrors}", endpoint.Description]
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

            UpdateTrafficTypeDependentControls();
        }

        private TrafficType SelectedTrafficType
            => comboBoxTrafficType.SelectedValue is TrafficType trafficType ? trafficType : TrafficType.Raw;

        private void DataGridViewEndpoints_DefaultValuesNeeded(object? sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells[ColumnEndpointsTrafficType.Index].Value = $"{SelectedTrafficType}";
        }

        private void ButtonBrowseCertificate_Click(object? sender, EventArgs e)
        {
            //This browses the machine that the console runs on. The path is read by the service, so it only
            //  makes sense to browse when that is the same machine; otherwise type the path on the service machine.
            using var dialog = new OpenFileDialog
            {
                Title = "Select the TLS certificate",
                Filter = "PKCS#12 certificate (*.pfx;*.p12)|*.pfx;*.p12|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textBoxCertificatePath.Text = dialog.FileName;
            }
        }

        private bool TryReadForm(out NpProxyConfiguration proxy)
        {
            proxy = new NpProxyConfiguration();

            try
            {
                textBoxProxyName.GetAndValidateText("The proxy name is required.");

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
                return false;
            }

            proxy.Id = _proxyId.EnsureNotNullOrEmpty();
            proxy.Name = textBoxProxyName.Text;
            proxy.Description = textBoxDescription.Text;
            proxy.TrafficType = SelectedTrafficType;
            proxy.BindingProtocol = comboBoxBindingProtocol.SelectedValue is BindingProtocol protocol ? protocol : BindingProtocol.IPv4;
            proxy.ListenPort = int.Parse(textBoxListenPort.Text);
            proxy.ListenOnAllAddresses = checkBoxListenOnAllAddresses.Checked;
            proxy.InitialBufferSize = int.Parse(textBoxInitialBufferSize.Text);
            proxy.MaxBufferSize = int.Parse(textBoxMaxBufferSize.Text);
            proxy.AcceptBacklogSize = int.Parse(textBoxAcceptBacklogSize.Text);
            proxy.Endpoints.ConnectionPattern = comboBoxConnectionPattern.SelectedValue is ConnectionPattern pattern ? pattern : ConnectionPattern.RoundRobbin;
            proxy.AutoStart = checkBoxListenAutoStart.Checked;
            proxy.UseStickySessions = checkBoxUseStickySessions.Checked;
            proxy.StickySessionCacheExpiration = int.Parse(textBoxStickySessionCacheExpiration.Text);
            proxy.TlsCertificatePath = textBoxCertificatePath.Text.Trim();
            proxy.TlsCertificatePassword = textBoxCertificatePassword.Text;

            if (proxy.InitialBufferSize > proxy.MaxBufferSize)
            {
                MessageBox.Show("The max buffer size must be larger than the initial buffer size.",
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            if (proxy.TrafficType == TrafficType.Https && string.IsNullOrWhiteSpace(proxy.TlsCertificatePath))
            {
                tabControl.SelectedTab = tabPageTls;
                MessageBox.Show("A certificate (PFX) is required to terminate HTTPS traffic. Use the Raw traffic type to pass encrypted traffic through untouched.",
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            foreach (DataGridViewRow row in dataGridViewEndpoints.Rows)
            {
                if (row.IsNewRow || ((string)(row.Cells[ColumnEndpointsAddress.Index].Value ?? "")).Trim() == string.Empty)
                {
                    continue;
                }

                var address = ((string)row.Cells[ColumnEndpointsAddress.Index].Value!).Trim();

                if (int.TryParse(row.Cells[ColumnEndpointsPort.Index].Value?.ToString(), out var port) == false || port < 1 || port > 65535)
                {
                    tabControl.SelectedTab = tabPageEndpoints;
                    dataGridViewEndpoints.CurrentCell = row.Cells[ColumnEndpointsPort.Index];
                    MessageBox.Show($"The port of endpoint '{address}' must be between 1 and 65535.",
                        Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return false;
                }

                var trafficType = Enum.TryParse<TrafficType>(row.Cells[ColumnEndpointsTrafficType.Index].Value?.ToString(), out var parsed)
                    ? parsed : proxy.TrafficType;

                proxy.Endpoints.Add(new NpEndpoint()
                {
                    Enabled = bool.Parse(row.Cells[ColumnEndpointsEnabled.Index].Value?.ToString() ?? "True"),
                    Address = address,
                    Port = port,
                    TrafficType = trafficType,
                    TlsIgnoreCertificateErrors = bool.Parse(row.Cells[ColumnEndpointsIgnoreCertificateErrors.Index].Value?.ToString() ?? "False"),
                    Description = row.Cells[ColumnEndpointsDescription.Index].Value?.ToString() ?? ""
                });
            }

            foreach (DataGridViewRow row in dataGridViewBindings.Rows)
            {
                if (row.IsNewRow || ((string)(row.Cells[ColumnBindingsIPAddress.Index].Value ?? "")).Trim() == string.Empty)
                {
                    continue;
                }

                var address = ((string)row.Cells[ColumnBindingsIPAddress.Index].Value!).Trim();

                //IPv6 addresses are often typed in square brackets.
                var unbracketed = address.StartsWith('[') && address.EndsWith(']') ? address[1..^1] : address;
                if (IPAddress.TryParse(unbracketed, out _) == false)
                {
                    tabControl.SelectedTab = tabPageBindings;
                    dataGridViewBindings.CurrentCell = row.Cells[ColumnBindingsIPAddress.Index];
                    MessageBox.Show($"'{address}' is not a valid IPv4 or IPv6 address.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return false;
                }

                proxy.Bindings.Add(new NpBinding()
                {
                    Enabled = bool.Parse((row.Cells[ColumnBindingsEnabled.Index].Value?.ToString()) ?? "True"),
                    Address = address,
                    Description = row.Cells[ColumnBindingsDescription.Index].Value?.ToString() ?? ""
                });
            }

            foreach (DataGridViewRow row in dataGridViewHTTPHeaders.Rows)
            {
                if (row.IsNewRow || ((string)(row.Cells[ColumnHTTPHeadersHeader.Index].Value ?? "")) == string.Empty)
                {
                    continue;
                }

                var headerType = Enum.Parse<HttpHeaderType>(row.Cells[ColumnHTTPHeadersType.Index].Value?.ToString() ?? "");
                var verb = Enum.Parse<HttpVerb>(row.Cells[ColumnHTTPHeadersVerb.Index].Value?.ToString() ?? "");

                if (headerType == HttpHeaderType.Response && verb != HttpVerb.Any)
                {
                    //A response does not carry the verb of the request it answers, so such a rule could never match.
                    tabControl.SelectedTab = tabPageHTTPHeaders;
                    dataGridViewHTTPHeaders.CurrentCell = row.Cells[ColumnHTTPHeadersVerb.Index];
                    MessageBox.Show($"The response header rule for '{row.Cells[ColumnHTTPHeadersHeader.Index].Value}' must use the verb 'Any'.",
                        Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return false;
                }

                proxy.HttpHeaderRules.Add(new NpHttpHeaderRule
                {
                    Enabled = bool.Parse(row.Cells[ColumnHTTPHeadersEnabled.Index].Value?.ToString() ?? "True"),
                    Action = Enum.Parse<HttpHeaderAction>(row.Cells[ColumnHTTPHeadersAction.Index].Value?.ToString() ?? ""),
                    Description = row.Cells[ColumnHTTPHeadersDescription.Index].Value?.ToString() ?? "",
                    HeaderType = headerType,
                    Name = row.Cells[ColumnHTTPHeadersHeader.Index].Value?.ToString() ?? "",
                    Value = row.Cells[ColumnHTTPHeadersValue.Index].Value?.ToString() ?? "",
                    Verb = verb
                });
            }

            if (proxy.Endpoints.Collection.Count == 0)
            {
                tabControl.SelectedTab = tabPageEndpoints;
                MessageBox.Show("At least one end-point is required.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            if (proxy.Endpoints.Collection.Any(o => o.Enabled) == false)
            {
                tabControl.SelectedTab = tabPageEndpoints;
                MessageBox.Show("At least one end-point must be enabled.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            if (proxy.Bindings.Count == 0 && proxy.ListenOnAllAddresses == false)
            {
                tabControl.SelectedTab = tabPageBindings;
                MessageBox.Show("At least one binding is required unless [listen on all addresses] is selected.",
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            return true;
        }

        private void ButtonSave_Click(object? sender, EventArgs e)
        {
            if (TryReadForm(out var proxy) == false)
            {
                return;
            }

            lock (_serviceMessages)
            {
                _serviceMessages.Clear();
            }

            _messageClient.EnsureNotNull().Notify(new NotificationUpsertProxy(proxy));

            //Give the service a moment to save, and to say if it could not do what was asked.
            Thread.Sleep(600);

            string? message;
            lock (_serviceMessages)
            {
                message = _serviceMessages.FirstOrDefault();
            }

            if (message != null)
            {
                MessageBox.Show(message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void FormProxy_FormClosed(object? sender, FormClosedEventArgs e)
        {
            _messageClient?.Disconnect();
        }

        private void ComboBoxTrafficType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var trafficType = SelectedTrafficType;

            //Endpoints that were using the traffic type of the listener keep doing so when it is changed.
            if (trafficType != _previousTrafficType)
            {
                foreach (DataGridViewRow row in dataGridViewEndpoints.Rows)
                {
                    if (row.IsNewRow == false && $"{row.Cells[ColumnEndpointsTrafficType.Index].Value}" == $"{_previousTrafficType}")
                    {
                        row.Cells[ColumnEndpointsTrafficType.Index].Value = $"{trafficType}";
                    }
                }
                _previousTrafficType = trafficType;
            }

            UpdateTrafficTypeDependentControls();
        }

        private void UpdateTrafficTypeDependentControls()
        {
            var trafficType = SelectedTrafficType;

            //Header rules apply to HTTP, and to HTTPS where the proxy terminates TLS.
            dataGridViewHTTPHeaders.ReadOnly = trafficType == TrafficType.Raw;

            textBoxCertificatePath.Enabled = trafficType == TrafficType.Https;
            textBoxCertificatePassword.Enabled = trafficType == TrafficType.Https;
            buttonBrowseCertificate.Enabled = trafficType == TrafficType.Https;
        }

        private void CheckBoxListenOnAllAddresses_CheckedChanged(object? sender, EventArgs e)
        {
            dataGridViewBindings.ReadOnly = checkBoxListenOnAllAddresses.Checked;
            comboBoxBindingProtocol.Enabled = checkBoxListenOnAllAddresses.Checked;
        }

        private void DataGridViewHTTPHeaders_Click(object? sender, EventArgs e)
        {
            if (dataGridViewHTTPHeaders.ReadOnly)
            {
                MessageBox.Show("HTTP header rules cannot be added because the traffic type is Raw. Use HTTP, or HTTPS with a certificate.",
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void DataGridViewBindings_Click(object? sender, EventArgs e)
        {
            if (dataGridViewBindings.ReadOnly)
            {
                MessageBox.Show("Bindings cannot be added because [listen on all addresses] is selected.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }
    }
}
