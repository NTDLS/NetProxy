using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
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
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
        }

        public FormProxy(ConnectionInfo connectionInfo, Guid? proxyId)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            InitializeHostNamesTab();
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
                new (Localize("Raw"), TrafficType.Raw),
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
            checkBoxListenAutoStart.Checked = true;
            //----------------------------------------------------------------------------

            _previousTrafficType = TrafficType.Http;
        }

        private DataGridView _hostNamesGrid = null!;
        private DataGridViewCheckBoxColumn _hostNamesEnabled = null!;
        private DataGridViewTextBoxColumn _hostNamesHost = null!;
        private DataGridViewTextBoxColumn _hostNamesCertificate = null!;
        private DataGridViewTextBoxColumn _hostNamesPassword = null!;
        private DataGridViewTextBoxColumn _hostNamesDescription = null!;
        private TabPage _tabPageHostNames = null!;

        /// <summary>
        /// The host names tab, which is where proxies that share a port are told apart (and given a certificate each).
        /// </summary>
        private void InitializeHostNamesTab()
        {
            _hostNamesEnabled = new DataGridViewCheckBoxColumn { HeaderText = Localize("Enabled"), Width = 55, Frozen = true };
            _hostNamesEnabled.DefaultCellStyle.NullValue = true;
            _hostNamesHost = new DataGridViewTextBoxColumn
            {
                HeaderText = Localize("Host name"),
                Width = 170,
                ToolTipText = Localize("A host name such as www.example.com, or *.example.com for every name below a domain.")
            };
            _hostNamesCertificate = new DataGridViewTextBoxColumn
            {
                HeaderText = Localize("Certificate (PFX)"),
                Width = 170,
                ToolTipText = Localize("HTTPS only. The certificate for this host name, on the machine that the service runs on (double-click to browse). When empty, the certificate of the proxy is used.")
            };
            _hostNamesPassword = new DataGridViewTextBoxColumn { HeaderText = Localize("Password"), Width = 80 };
            _hostNamesDescription = new DataGridViewTextBoxColumn { HeaderText = Localize("Description"), Width = 120 };

            _hostNamesGrid = new DataGridView
            {
                Location = new Point(9, 66),
                Size = new Size(579, 253),
                BackgroundColor = SystemColors.ButtonFace,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _hostNamesGrid.Columns.AddRange(_hostNamesEnabled, _hostNamesHost, _hostNamesCertificate, _hostNamesPassword, _hostNamesDescription);
            _hostNamesGrid.DataError += (_, e) => e.ThrowException = false;

            //The password is not shown, in the grid or while it is typed.
            _hostNamesGrid.CellFormatting += (_, e) =>
            {
                if (e.ColumnIndex == _hostNamesPassword.Index && string.IsNullOrEmpty(e.Value as string) == false)
                {
                    e.Value = "••••••";
                    e.FormattingApplied = true;
                }
            };
            _hostNamesGrid.EditingControlShowing += (_, e) =>
            {
                if (e.Control is TextBox textBox)
                {
                    textBox.UseSystemPasswordChar = _hostNamesGrid.CurrentCell?.ColumnIndex == _hostNamesPassword.Index;
                }
            };
            _hostNamesGrid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex != _hostNamesCertificate.Index || _hostNamesGrid.ReadOnly)
                {
                    return;
                }

                using var dialog = new OpenFileDialog
                {
                    Title = Localize("Select the TLS certificate"),
                    Filter = Localize("PKCS#12 certificate (*.pfx;*.p12)|*.pfx;*.p12|All files (*.*)|*.*")
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _hostNamesGrid.Rows[e.RowIndex].Cells[_hostNamesCertificate.Index].Value = dialog.FileName;
                }
            };

            var info = new Label
            {
                Location = new Point(9, 9),
                Size = new Size(579, 54),
                Text = Localize("Several HTTP or HTTPS proxies can listen on the same port if they answer to different host names. A request goes to the proxy with the host name it asks for"
                    + " (the Host header, or the server name of the TLS handshake). A proxy with no host names gets the requests that no other proxy claims. Raw proxies cannot share a port.")
            };

            _tabPageHostNames = new TabPage
            {
                Text = Localize("Host names"),
                BackColor = SystemColors.Control,
                Padding = new Padding(3)
            };
            _tabPageHostNames.Controls.Add(info);
            _tabPageHostNames.Controls.Add(_hostNamesGrid);

            tabControl.TabPages.Insert(tabControl.TabPages.IndexOf(tabPageBindings) + 1, _tabPageHostNames);
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
                Localize("What the listener accepts. Raw passes any TCP protocol through untouched. HTTP lets the HTTP header rules change the requests and responses."
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

            toolTips.AddControls([labelCertificatePath, textBoxCertificatePath],
                Localize("The PFX (PKCS#12) certificate that the listener presents to clients, for the HTTPS traffic type. The path is read by the service,"
                + " so it is a path on the machine that the service runs on."));

            toolTips.AddControls([buttonBrowseCertificate],
                Localize("Browse this machine for the certificate. That only helps when the service runs on this machine."));

            toolTips.AddControls([labelCertificatePassword, textBoxCertificatePassword],
                Localize("The password of the PFX certificate. Leave it empty if the certificate has none. The service stores it encrypted."));

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
            ColumnEndpointsTrafficType.ToolTipText = Localize("What is spoken to this endpoint. HTTPS starts TLS with it. Raw and HTTP are sent as they are."
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
                Localize("A connection that has carried no data in either direction for this many seconds is closed. 0 is no timeout."));

            toolTips.AddControls([label15, textBoxStickySessionCacheExpiration],
                Localize("How long, in seconds, a client is remembered by sticky sessions after it was last connected."));

            #endregion
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
            //Proxies saved with a smaller backlog have been using the minimum, so that is what they are shown with.
            textBoxAcceptBacklogSize.Text = $"{(proxy.AcceptBacklogSize > 0 ? Math.Max(proxy.AcceptBacklogSize, Constants.MinAcceptBacklogSize) : 0)}";
            comboBoxConnectionPattern.SelectedValue = proxy.Endpoints.ConnectionPattern;
            checkBoxListenAutoStart.Checked = proxy.AutoStart;
            checkBoxUseStickySessions.Checked = proxy.UseStickySessions;
            textBoxStickySessionCacheExpiration.Text = $"{proxy.StickySessionCacheExpiration}";
            textBoxMaxConnections.Text = $"{proxy.MaxConnections}";
            textBoxMaxConnectionsPerClient.Text = $"{proxy.MaxConnectionsPerClient}";
            textBoxIdleTimeout.Text = $"{proxy.IdleTimeoutSeconds}";
            checkBoxAllowHttp2.Checked = proxy.AllowHttp2;
            textBoxCertificatePath.Text = proxy.TlsCertificatePath;
            textBoxCertificatePassword.Text = proxy.TlsCertificatePassword;

            foreach (var endpoint in proxy.Endpoints.Collection)
            {
                dataGridViewEndpoints.Rows.Add(
                    [$"{endpoint.Enabled}", endpoint.Address, $"{endpoint.Port}", $"{endpoint.TrafficType}", $"{endpoint.TlsValidateSSLCertificate}", endpoint.Description]
                );
            }

            foreach (var host in proxy.HostNames)
            {
                _hostNamesGrid.Rows.Add(
                    [$"{host.Enabled}", host.Host, host.TlsCertificatePath, host.TlsCertificatePassword, host.Description]
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
                Title = Localize("Select the TLS certificate"),
                Filter = Localize("PKCS#12 certificate (*.pfx;*.p12)|*.pfx;*.p12|All files (*.*)|*.*")
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
            proxy.AllowHttp2 = checkBoxAllowHttp2.Checked;
            proxy.TlsCertificatePath = textBoxCertificatePath.Text.Trim();
            proxy.TlsCertificatePassword = textBoxCertificatePassword.Text;

            if (proxy.InitialBufferSize > proxy.MaxBufferSize)
            {
                MessageBox.Show(Localize("The max buffer size must be larger than the initial buffer size."),
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return false;
            }

            foreach (DataGridViewRow row in _hostNamesGrid.Rows)
            {
                var host = ((string)(row.Cells[_hostNamesHost.Index].Value ?? "")).Trim();
                if (row.IsNewRow || host == string.Empty)
                {
                    continue;
                }

                var hostPattern = NpHostMatcher.Normalize(host);
                if (NpHostMatcher.IsValidPattern(hostPattern) == false)
                {
                    tabControl.SelectedTab = _tabPageHostNames;
                    _hostNamesGrid.CurrentCell = row.Cells[_hostNamesHost.Index];
                    MessageBox.Show(Localize("'{0}' is not a valid host name (use a name like www.example.com, or *.example.com for all names below a domain).", host),
                        Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return false;
                }

                if (proxy.HostNames.Any(o => o.Host == hostPattern))
                {
                    tabControl.SelectedTab = _tabPageHostNames;
                    _hostNamesGrid.CurrentCell = row.Cells[_hostNamesHost.Index];
                    MessageBox.Show(Localize("The host name '{0}' is listed more than once.", hostPattern!), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return false;
                }

                proxy.HostNames.Add(new NpHostName
                {
                    Enabled = bool.Parse(row.Cells[_hostNamesEnabled.Index].Value?.ToString() ?? "True"),
                    Host = hostPattern!,
                    TlsCertificatePath = (row.Cells[_hostNamesCertificate.Index].Value?.ToString() ?? "").Trim(),
                    TlsCertificatePassword = row.Cells[_hostNamesPassword.Index].Value?.ToString() ?? "",
                    Description = row.Cells[_hostNamesDescription.Index].Value?.ToString() ?? ""
                });
            }

            if (proxy.TrafficType == TrafficType.Https && string.IsNullOrWhiteSpace(proxy.TlsCertificatePath)
                && proxy.HostNames.Any(o => o.Enabled && string.IsNullOrWhiteSpace(o.TlsCertificatePath) == false) == false)
            {
                tabControl.SelectedTab = tabPageTls;
                MessageBox.Show(Localize("A certificate (PFX) is required to terminate HTTPS traffic. Use the Raw traffic type to pass encrypted traffic through untouched."),
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

            //Raw traffic has no host name to tell proxies apart by, so it cannot share a port.
            _hostNamesGrid.ReadOnly = trafficType == TrafficType.Raw;
            _hostNamesCertificate.ReadOnly = trafficType != TrafficType.Https;
            _hostNamesPassword.ReadOnly = trafficType != TrafficType.Https;
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
                MessageBox.Show(Localize("HTTP header rules cannot be added because the traffic type is Raw. Use HTTP, or HTTPS with a certificate."),
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
