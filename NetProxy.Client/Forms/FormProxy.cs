using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.Helpers;
using NTDLS.Localization;
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
        private delegate void PopulateProxyInformation(NpProxyConfiguration proxy);
        private PopulateProxyInformation? _populateProxyInformation;

        /// <summary>
        /// The traffic type of the listener before it was last changed, so that endpoints that followed it can follow it again.
        /// </summary>
        private TrafficType _previousTrafficType = TrafficType.Http;

        public FormProxy()
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
        }

        public FormProxy(ConnectionInfo connectionInfo, Guid? proxyId)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
            SetupToolTips();

            _isNewProxy = proxyId == null;

            _populateProxyInformation = OnPopulateProxyInformation;
            _proxyId = proxyId ?? Guid.NewGuid();

            _messageClient = MessageClientFactory.Create(connectionInfo, out var error);
            if (_messageClient == null)
            {
                MessageBox.Show(error ?? Localize("Could not connect to the service."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                Close();
                return;
            }


            var trafficTypes = new List<ComboItem>
            {
                new ("TCP", TrafficType.Tcp),
                new ("HTTP", TrafficType.Http),
                new ("HTTPS", TrafficType.Https),
                new ("UDP", TrafficType.Udp)
            };

            comboBoxTrafficType.DisplayMember = "Display";
            comboBoxTrafficType.ValueMember = "Value";
            comboBoxTrafficType.DataSource = trafficTypes;

            var bindingProtocol = new List<ComboItem>
            {
                new ("IPv4", BindingProtocol.IPv4),
                new ("IPv6", BindingProtocol.IPv6),
                new (Localize("IPv4 and IPv6"), BindingProtocol.Both)
            };

            comboBoxBindingProtocol.DisplayMember = "Display";
            comboBoxBindingProtocol.ValueMember = "Value";
            comboBoxBindingProtocol.DataSource = bindingProtocol;

            var connectionPatterns = new List<ComboItem>
            {
                new (Localize("Balanced"), ConnectionPattern.Balanced),
                new (Localize("Fail-Over"), ConnectionPattern.FailOver),
                new (Localize("Round-Robbin"), ConnectionPattern.RoundRobbin)
            };

            comboBoxConnectionPattern.DisplayMember = "Display";
            comboBoxConnectionPattern.ValueMember = "Value";
            comboBoxConnectionPattern.DataSource = connectionPatterns;

            var forwardedHeaders = new List<ComboItem>
            {
                new (Localize("Off"), ForwardedHeadersMode.Off),
                new (Localize("Set (replace what the client sent)"), ForwardedHeadersMode.Replace),
                new (Localize("Append (behind another proxy)"), ForwardedHeadersMode.Append)
            };

            comboBoxForwardedHeaders.DisplayMember = "Display";
            comboBoxForwardedHeaders.ValueMember = "Value";
            comboBoxForwardedHeaders.DataSource = forwardedHeaders;

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
            textBoxMaxConnections.Text = "0";
            textBoxMaxConnectionsPerClient.Text = "0";
            textBoxIdleTimeout.Text = "0";
            textBoxMaxDatagrams.Text = "0";
            checkBoxListenAutoStart.Checked = true;
            //----------------------------------------------------------------------------

            _previousTrafficType = TrafficType.Http;
        }

        /// <summary>
        /// The headers of a grid are not translated by FormLocalizer.
        /// </summary>
        private void LocalizeGrids()
        {
            ColumnBindingsEnabled.HeaderText = Localize("Enabled");
            ColumnBindingsIPAddress.HeaderText = Localize("IP Address");
            ColumnBindingsDescription.HeaderText = Localize("Description");
            ColumnHTTPHeadersEnabled.HeaderText = Localize("Enabled");
            ColumnHTTPHeadersType.HeaderText = Localize("Type");
            ColumnHTTPHeadersVerb.HeaderText = Localize("Verb");
            ColumnHTTPHeadersHeader.HeaderText = Localize("Header");
            ColumnHTTPHeadersAction.HeaderText = Localize("Action");
            ColumnHTTPHeadersValue.HeaderText = Localize("Value");
            ColumnHTTPHeadersDescription.HeaderText = Localize("Description");
            ColumnEndpointsEnabled.HeaderText = Localize("Enabled");
            ColumnEndpointsAddress.HeaderText = Localize("Address");
            ColumnEndpointsPort.HeaderText = Localize("Port");
            ColumnEndpointsTrafficType.HeaderText = Localize("Traffic");
            ColumnEndpointsValidateSSLCertificate.HeaderText = Localize("Validate Certificate");
            ColumnEndpointsDescription.HeaderText = Localize("Description");
            ColumnHostNamesEnabled.HeaderText = Localize("Enabled");
            ColumnHostNamesHost.HeaderText = Localize("Host name");
            ColumnHostNamesCertificate.HeaderText = Localize("Certificate");
            ColumnHostNamesDescription.HeaderText = Localize("Description");
        }

        /// <summary>
        /// Tool-tips for the inputs: the label and the control that it is for show the same text, and so do the columns of the grids.
        /// </summary>
        private void SetupToolTips()
        {
            var toolTips = ToolTipHelpers.CreateToolTipControl(this);

            #region General.

            toolTips.AddControls([label1, textBoxProxyName],
                Localize("A name for this proxy. It is only for your own identification."));

            toolTips.AddControls([label2, comboBoxTrafficType],
                Localize("What the listener accepts. TCP passes any TCP protocol through untouched. HTTP lets the HTTP header rules change the requests and responses."
                + " HTTPS terminates TLS with the certificate on the TLS tab, so that the header rules can be applied to HTTPS too."));

            toolTips.AddControls([checkBoxAllowHttp2],
                Localize("Offer HTTP/2 to clients, which is also what gRPC needs. With HTTPS the endpoint has to agree to HTTP/2 as well (TLS to the endpoint)."
                + " With HTTP, a client that starts with HTTP/2 without TLS (h2c) is detected, and the endpoint has to accept that. Header rules apply to HTTP/2."));

            toolTips.AddControls([checkBoxListenAutoStart],
                Localize("Start this proxy when the service starts."));

            toolTips.AddControls([label6, textBoxDescription],
                Localize("Notes about this proxy. They are only for you."));

            #endregion

            #region Bindings.

            toolTips.AddControls([label4, textBoxListenPort],
                Localize("The port that the proxy listens on (1 to 65535)."));

            toolTips.AddControls([label10, comboBoxBindingProtocol],
                Localize("The IP version to listen on when listening on all addresses. IPv4 and IPv6 is one listener that accepts both."
                + " Bindings that are listed use the IP version of their address."));

            toolTips.AddControls([checkBoxListenOnAllAddresses],
                Localize("Listen on every address of the machine that the service runs on. Clear it to listen only on the addresses in the list."));

            toolTips.AddControls([label7],
                Localize("The addresses to listen on, IPv4 or IPv6 (for example 192.168.1.5 or ::1). Only used when [Listen on All Addresses] is cleared."));

            ColumnBindingsEnabled.ToolTipText = Localize("Listen on this address. Clear it to keep the address without using it.");
            ColumnBindingsIPAddress.ToolTipText = Localize("An IPv4 or IPv6 address of the machine that the service runs on.");
            ColumnBindingsDescription.ToolTipText = Localize("Notes about this address.");

            #endregion

            #region TLS.

            toolTips.AddControls([labelCertificate, comboBoxCertificate],
                Localize("The certificate that the listener presents to clients, for the HTTPS traffic type. Certificates are set up once for the service"
                + " (Settings, Certificates) and are chosen by name here."));

            #endregion

            #region HTTP headers.

            ColumnHTTPHeadersEnabled.ToolTipText = Localize("Apply this rule. Clear it to keep the rule without using it.");
            ColumnHTTPHeadersType.ToolTipText = Localize("Request rules change what clients send, at the listener. Response rules change what the endpoint answers. Any does both.");
            ColumnHTTPHeadersVerb.ToolTipText = Localize("The request method that the rule applies to. A response does not carry one, so response rules must use Any.");
            ColumnHTTPHeadersHeader.ToolTipText = Localize("The name of the header, for example Host. It is not case sensitive.");
            ColumnHTTPHeadersAction.ToolTipText = Localize("Insert adds the header if it is missing. Update changes it if it is there. Upsert does both. Delete removes it.");
            ColumnHTTPHeadersValue.ToolTipText = Localize("The value that is written. It is used as typed. Delete does not use it.");
            ColumnHTTPHeadersDescription.ToolTipText = Localize("Notes about this rule.");

            #endregion

            #region Endpoints.

            toolTips.AddControls([label9, comboBoxConnectionPattern],
                Localize("How a new connection chooses an endpoint. Balanced starts with the endpoint that has the fewest connections. Fail-over always tries the endpoints"
                + " in the order listed. Round-robin takes them in turn. An endpoint that cannot be reached is skipped."));

            toolTips.AddControls([checkBoxUseStickySessions],
                Localize("Send a client back to the endpoint it used last, for as long as the sticky session expiration (on the Advanced tab) has not passed since it was last used."));

            toolTips.AddControls([label8],
                Localize("The machines that connections are passed to. Each endpoint has its own traffic type."));

            ColumnEndpointsEnabled.ToolTipText = Localize("Use this endpoint. Clear it to keep the endpoint without using it.");
            ColumnEndpointsAddress.ToolTipText = Localize("A host name, or an IPv4 or IPv6 address. For HTTPS use the host name, which is what the certificate is checked against.");
            ColumnEndpointsPort.ToolTipText = Localize("The port on the endpoint (1 to 65535).");
            ColumnEndpointsTrafficType.ToolTipText = Localize("What is spoken to this endpoint. HTTPS starts TLS with it. TCP and HTTP are sent as they are."
                + " A new endpoint starts with the traffic type of the listener.");
            ColumnEndpointsValidateSSLCertificate.ToolTipText = Localize("For HTTPS endpoints: check that the certificate of the endpoint is trusted and is for its address."
                + " Clear it to accept any certificate, for example a self signed one.");
            ColumnEndpointsDescription.ToolTipText = Localize("Notes about this endpoint.");

            #endregion

            #region Advanced.

            toolTips.AddControls([label3, textBoxInitialBufferSize],
                Localize("The size in bytes that the buffer of each connection starts at (1024 or more)."));

            toolTips.AddControls([label5, textBoxMaxBufferSize],
                Localize("The largest the buffer of a connection grows to, when it keeps filling up. It cannot be smaller than the initial size."));

            toolTips.AddControls([labelAcceptBacklogSize, textBoxAcceptBacklogSize],
                Localize("How many connections can be waiting to be accepted. The smallest is 128. 0 uses the largest the system allows."));

            toolTips.AddControls([labelMaxConnections, textBoxMaxConnections],
                Localize("The most connections the proxy has open at once. Further connections are closed as they arrive. 0 is no limit."));

            toolTips.AddControls([labelMaxConnectionsPerClient, textBoxMaxConnectionsPerClient],
                Localize("The most connections that one client address has open at once. Further connections from it are closed. 0 is no limit."));

            toolTips.AddControls([labelIdleTimeout, textBoxIdleTimeout],
                Localize("A connection that has carried no data in either direction for this many seconds is closed. 0 is no timeout."
                + " For UDP it is how long a client's session is kept without datagrams, and 0 is 60 seconds."));

            toolTips.AddControls([label15, textBoxStickySessionCacheExpiration],
                Localize("How long, in seconds, a client is remembered by sticky sessions after it was last connected."));

            toolTips.AddControls([labelMaxDatagrams, textBoxMaxDatagrams],
                Localize("UDP only. The most datagrams per second that one client (address and port) may send. The ones above that are dropped. 0 is no limit."));

            toolTips.AddControls([labelForwardedHeaders, comboBoxForwardedHeaders],
                Localize("Tell the endpoint who the client is, which it cannot see when the connection comes from the proxy. Set: X-Forwarded-For is the client address"
                + " and X-Forwarded-Proto is http or https, replacing anything the client sent. Append: for a proxy behind another proxy, the client is added to the end of X-Forwarded-For."));

            ColumnHostNamesHost.ToolTipText = Localize("A host name such as www.example.com, or *.example.com for every name below a domain.");
            ColumnHostNamesCertificate.ToolTipText = Localize("HTTPS only. The certificate for this host name, chosen from the certificates of the service."
                + " When empty, the certificate of the proxy is used.");

            #endregion
        }

        private List<NpPluginGridItem> _servicePlugins = new();

        private List<NpCertificateGridItem> _serviceCertificates = new();

        private void LoadServiceCertificates()
        {
            try
            {
                _serviceCertificates = _messageClient.EnsureNotNull().Query<QueryCertificateListReply>(new QueryCertificateList()).Collection;
            }
            catch
            {
                _serviceCertificates = new();
            }

            FillCertificateChoices([]);
        }

        /// <summary>
        /// The certificates that can be chosen: none, and the ones of the service. A name that the proxy has but the service has not
        /// (it was deleted) is kept in the list, so that it is shown instead of lost.
        /// </summary>
        private void FillCertificateChoices(IEnumerable<string> current)
        {
            var names = new List<string> { string.Empty };
            names.AddRange(_serviceCertificates.Select(o => o.Name));

            foreach (var name in current.Where(o => string.IsNullOrWhiteSpace(o) == false))
            {
                if (names.Contains(name, StringComparer.OrdinalIgnoreCase) == false)
                {
                    names.Add(name);
                }
            }

            comboBoxCertificate.DataSource = names;
            ColumnHostNamesCertificate.Items.Clear();
            ColumnHostNamesCertificate.Items.AddRange(names.Cast<object>().ToArray());
        }

        private void DataGridViewHostNames_DataError(object? sender, DataGridViewDataErrorEventArgs e)
            => e.ThrowException = false;

        private void LoadServicePlugins()
        {
            try
            {
                _servicePlugins = _messageClient.EnsureNotNull().Query<QueryPluginListReply>(new QueryPluginList()).Collection;
            }
            catch
            {
                _servicePlugins = new();
            }

            checkedListBoxPlugins.Items.Clear();
            foreach (var plugin in _servicePlugins)
            {
                var note = plugin.Enabled == false ? $" ({Localize("disabled")})" : (plugin.IsLoaded ? "" : $" ({Localize("not loaded")})");
                checkedListBoxPlugins.Items.Add(new PluginItem(plugin.Id, plugin.Name + note));
            }
        }

        private sealed record PluginItem(Guid Id, string Text)
        {
            public override string ToString() => Text;
        }

        private void FormProxy_Shown(object? sender, EventArgs e)
        {
            LoadServicePlugins();
            LoadServiceCertificates();

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
            //Proxies saved with a smaller backlog have been using the minimum, so that is what they are shown with.
            textBoxAcceptBacklogSize.Text = $"{(proxy.AcceptBacklogSize > 0 ? Math.Max(proxy.AcceptBacklogSize, Constants.MinAcceptBacklogSize) : 0)}";
            comboBoxConnectionPattern.SelectedValue = proxy.Endpoints.ConnectionPattern;
            checkBoxListenAutoStart.Checked = proxy.AutoStart;
            checkBoxUseStickySessions.Checked = proxy.UseStickySessions;
            textBoxStickySessionCacheExpiration.Text = $"{proxy.StickySessionCacheExpiration}";
            textBoxMaxConnections.Text = $"{proxy.MaxConnections}";
            textBoxMaxConnectionsPerClient.Text = $"{proxy.MaxConnectionsPerClient}";
            textBoxIdleTimeout.Text = $"{proxy.IdleTimeoutSeconds}";
            textBoxMaxDatagrams.Text = $"{proxy.MaxDatagramsPerSecondPerClient}";
            checkBoxAllowHttp2.Checked = proxy.AllowHttp2;
            comboBoxForwardedHeaders.SelectedValue = proxy.ForwardedHeaders;
            FillCertificateChoices(proxy.HostNames.Select(o => o.TlsCertificate).Append(proxy.TlsCertificate));
            comboBoxCertificate.SelectedItem = string.IsNullOrWhiteSpace(proxy.TlsCertificate) ? string.Empty : proxy.TlsCertificate;

            foreach (var endpoint in proxy.Endpoints.Collection)
            {
                dataGridViewEndpoints.Rows.Add(
                    [$"{endpoint.Enabled}", endpoint.Address, $"{endpoint.Port}", $"{endpoint.TrafficType}", $"{endpoint.TlsValidateSSLCertificate}", endpoint.Description]
                );
            }

            for (var i = 0; i < checkedListBoxPlugins.Items.Count; i++)
            {
                checkedListBoxPlugins.SetItemChecked(i, proxy.PluginIds.Contains(((PluginItem)checkedListBoxPlugins.Items[i]).Id));
            }

            foreach (var host in proxy.HostNames)
            {
                dataGridViewHostNames.Rows.Add(
                    [$"{host.Enabled}", host.Host, host.TlsCertificate, host.Description]
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
            => comboBoxTrafficType.SelectedValue is TrafficType trafficType ? trafficType : TrafficType.Tcp;

        private void DataGridViewEndpoints_DefaultValuesNeeded(object? sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells[ColumnEndpointsTrafficType.Index].Value = $"{SelectedTrafficType}";
        }

        private bool TryReadForm(out NpProxyConfiguration proxy)
        {
            proxy = new NpProxyConfiguration();

            try
            {
                textBoxProxyName.GetAndValidateText(Localize("The proxy name is required."));

                textBoxListenPort.GetAndValidateNumeric(1, 65535,
                    Localize("The listen port is required (between [min] and [max])."));

                textBoxInitialBufferSize.GetAndValidateNumeric(1024, 1073741824,
                    Localize("The initial buffer size is required (between [min] and [max]."));

                textBoxMaxBufferSize.GetAndValidateNumeric(1024, 1073741824,
                    Localize("The max buffer size is required (between [min] and [max]."));

                var backlog = textBoxAcceptBacklogSize.GetAndValidateNumeric(0, 10000,
                    Localize("The accept backlog size is required (between [min] and [max])."));

                if (backlog is > 0 and < Constants.MinAcceptBacklogSize)
                {
                    throw new Exception(Localize("The accept backlog size must be 0 or at least {0}.", Constants.MinAcceptBacklogSize));
                }

                textBoxStickySessionCacheExpiration.GetAndValidateNumeric(1, 2592000,
                    Localize("The sticky session cache expiration (s) is required (between [min] and [max])."));

                textBoxMaxConnections.GetAndValidateNumeric(0, 1000000,
                    Localize("The max connections must be between [min] and [max] (0 is no limit)."));

                textBoxMaxConnectionsPerClient.GetAndValidateNumeric(0, 1000000,
                    Localize("The max connections per client must be between [min] and [max] (0 is no limit)."));

                textBoxIdleTimeout.GetAndValidateNumeric(0, 31536000,
                    Localize("The idle timeout (s) must be between [min] and [max] (0 is no timeout)."));

                textBoxMaxDatagrams.GetAndValidateNumeric(0, 100000000,
                    Localize("The most datagrams per second must be between [min] and [max] (0 is no limit)."));
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
            proxy.MaxConnections = int.Parse(textBoxMaxConnections.Text);
            proxy.MaxConnectionsPerClient = int.Parse(textBoxMaxConnectionsPerClient.Text);
            proxy.IdleTimeoutSeconds = int.Parse(textBoxIdleTimeout.Text);
            proxy.MaxDatagramsPerSecondPerClient = int.Parse(textBoxMaxDatagrams.Text);
            proxy.AllowHttp2 = checkBoxAllowHttp2.Checked;
            proxy.ForwardedHeaders = comboBoxForwardedHeaders.SelectedValue is ForwardedHeadersMode forwarded ? forwarded : ForwardedHeadersMode.Off;
            proxy.TlsCertificate = comboBoxCertificate.SelectedItem as string ?? string.Empty;

            if (proxy.InitialBufferSize > proxy.MaxBufferSize)
            {
                MessageBox.Show(Localize("The max buffer size must be larger than the initial buffer size."),
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            foreach (DataGridViewRow row in dataGridViewHostNames.Rows)
            {
                var host = ((string)(row.Cells[ColumnHostNamesHost.Index].Value ?? "")).Trim();
                if (row.IsNewRow || host == string.Empty)
                {
                    continue;
                }

                var hostPattern = NpHostMatcher.Normalize(host);
                if (NpHostMatcher.IsValidPattern(hostPattern) == false)
                {
                    tabControl.SelectedTab = tabPageHostNames;
                    dataGridViewHostNames.CurrentCell = row.Cells[ColumnHostNamesHost.Index];
                    MessageBox.Show(Localize("'{0}' is not a valid host name (use a name like www.example.com, or *.example.com for all names below a domain).", host),
                        Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return false;
                }

                if (proxy.HostNames.Any(o => o.Host == hostPattern))
                {
                    tabControl.SelectedTab = tabPageHostNames;
                    dataGridViewHostNames.CurrentCell = row.Cells[ColumnHostNamesHost.Index];
                    MessageBox.Show(Localize("The host name '{0}' is listed more than once.", hostPattern!), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return false;
                }

                proxy.HostNames.Add(new NpHostName
                {
                    Enabled = bool.Parse(row.Cells[ColumnHostNamesEnabled.Index].Value?.ToString() ?? "True"),
                    Host = hostPattern!,
                    TlsCertificate = (row.Cells[ColumnHostNamesCertificate.Index].Value?.ToString() ?? "").Trim(),
                    Description = row.Cells[ColumnHostNamesDescription.Index].Value?.ToString() ?? ""
                });
            }

            foreach (PluginItem item in checkedListBoxPlugins.CheckedItems)
            {
                proxy.PluginIds.Add(item.Id);
            }

            if (proxy.TrafficType == TrafficType.Https && string.IsNullOrWhiteSpace(proxy.TlsCertificate)
                && proxy.HostNames.Any(o => o.Enabled && string.IsNullOrWhiteSpace(o.TlsCertificate) == false) == false)
            {
                tabControl.SelectedTab = tabPageTls;
                MessageBox.Show(Localize("A certificate is required to terminate HTTPS traffic (set certificates up under Settings, Certificates). Use the TCP traffic type to pass encrypted traffic through untouched."),
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
                    MessageBox.Show(Localize("The port of endpoint '{0}' must be between 1 and 65535.", address),
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
                    TlsValidateSSLCertificate = bool.Parse(row.Cells[ColumnEndpointsValidateSSLCertificate.Index].Value?.ToString() ?? "False"),
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
                    MessageBox.Show(Localize("'{0}' is not a valid IPv4 or IPv6 address.", address), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
                    MessageBox.Show(Localize("The response header rule for '{0}' must use the verb 'Any'.", row.Cells[ColumnHTTPHeadersHeader.Index].Value ?? ""),
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
                MessageBox.Show(Localize("At least one end-point is required."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            if (proxy.Endpoints.Collection.Any(o => o.Enabled) == false)
            {
                tabControl.SelectedTab = tabPageEndpoints;
                MessageBox.Show(Localize("At least one end-point must be enabled."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            if (proxy.Bindings.Count == 0 && proxy.ListenOnAllAddresses == false)
            {
                tabControl.SelectedTab = tabPageBindings;
                MessageBox.Show(Localize("At least one binding is required unless [listen on all addresses] is selected."),
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

            QueryUpsertProxyReply reply;

            try
            {
                reply = _messageClient.EnsureNotNull().Query<QueryUpsertProxyReply>(new QueryUpsertProxy { Proxy = proxy, MatchByName = false });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Localize("The proxy was not saved: {0}", ex.GetBaseException().Message),
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (reply.Success == false)
            {
                //Nothing was saved, and the form stays as it is, so that the mistake can be fixed without starting over.
                MessageBox.Show(Localize("The proxy was not saved. {0}", reply.Message ?? string.Empty),
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (string.IsNullOrEmpty(reply.StartError) == false)
            {
                MessageBox.Show(Localize("The proxy was saved but could not be started: {0}", reply.StartError),
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
            var isHttp = trafficType is TrafficType.Http or TrafficType.Https;
            dataGridViewHTTPHeaders.ReadOnly = isHttp == false;

            comboBoxCertificate.Enabled = trafficType == TrafficType.Https;

            //TCP and UDP traffic have no host name to tell proxies apart by, so they cannot share a port. HTTP/2 is HTTP.
            comboBoxForwardedHeaders.Enabled = isHttp;
            checkBoxAllowHttp2.Enabled = isHttp;
            dataGridViewHostNames.ReadOnly = isHttp == false;
            ColumnHostNamesCertificate.ReadOnly = trafficType != TrafficType.Https;

            //Only UDP has sessions made of datagrams.
            textBoxMaxDatagrams.Enabled = trafficType == TrafficType.Udp;
            labelMaxDatagrams.Enabled = trafficType == TrafficType.Udp;
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
                MessageBox.Show(Localize("HTTP header rules cannot be added because the traffic type is TCP or UDP. Use HTTP, or HTTPS with a certificate."),
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void DataGridViewBindings_Click(object? sender, EventArgs e)
        {
            if (dataGridViewBindings.ReadOnly)
            {
                MessageBox.Show(Localize("Bindings cannot be added because [listen on all addresses] is selected."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }
    }
}
