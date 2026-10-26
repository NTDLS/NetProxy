using Microsoft.VisualBasic.ApplicationServices;
using NetProxy.Client.Properties;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.Design.AxImporter;
using System.Diagnostics;
using System.Windows.Forms.DataVisualization.Charting;
using System;

namespace NetProxy.Client.Forms
{
    partial class FormProxy
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormProxy));
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            label5 = new Label();
            labelAcceptBacklogSize = new Label();
            label7 = new Label();
            checkBoxListenOnAllAddresses = new CheckBox();
            tabControl = new TabControl();
            tabPageGeneral = new TabPage();
            textBoxDescription = new TextBox();
            label6 = new Label();
            checkBoxAllowHttp2 = new CheckBox();
            checkBoxListenAutoStart = new CheckBox();
            comboBoxTrafficType = new ComboBox();
            textBoxProxyName = new TextBox();
            tabPageBindings = new TabPage();
            comboBoxBindingProtocol = new ComboBox();
            label10 = new Label();
            dataGridViewBindings = new DataGridView();
            ColumnBindingsEnabled = new DataGridViewCheckBoxColumn();
            ColumnBindingsIPAddress = new DataGridViewTextBoxColumn();
            ColumnBindingsDescription = new DataGridViewTextBoxColumn();
            textBoxListenPort = new TextBox();
            tabPageHostNames = new TabPage();
            labelHostNamesInfo = new Label();
            dataGridViewHostNames = new DataGridView();
            ColumnHostNamesEnabled = new DataGridViewCheckBoxColumn();
            ColumnHostNamesHost = new DataGridViewTextBoxColumn();
            ColumnHostNamesCertificate = new DataGridViewComboBoxColumn();
            ColumnHostNamesDescription = new DataGridViewTextBoxColumn();
            tabPageTls = new TabPage();
            labelTlsInfo = new Label();
            labelCertificate = new Label();
            comboBoxCertificate = new ComboBox();
            tabPageHTTPHeaders = new TabPage();
            dataGridViewHTTPHeaders = new DataGridView();
            ColumnHTTPHeadersEnabled = new DataGridViewCheckBoxColumn();
            ColumnHTTPHeadersType = new DataGridViewComboBoxColumn();
            ColumnHTTPHeadersVerb = new DataGridViewComboBoxColumn();
            ColumnHTTPHeadersHeader = new DataGridViewTextBoxColumn();
            ColumnHTTPHeadersAction = new DataGridViewComboBoxColumn();
            ColumnHTTPHeadersValue = new DataGridViewTextBoxColumn();
            ColumnHTTPHeadersDescription = new DataGridViewTextBoxColumn();
            panelForwardedHeaders = new Panel();
            labelForwardedHeaders = new Label();
            comboBoxForwardedHeaders = new ComboBox();
            tabPageEndpoints = new TabPage();
            checkBoxUseStickySessions = new CheckBox();
            dataGridViewEndpoints = new DataGridView();
            ColumnEndpointsEnabled = new DataGridViewCheckBoxColumn();
            ColumnEndpointsAddress = new DataGridViewTextBoxColumn();
            ColumnEndpointsPort = new DataGridViewTextBoxColumn();
            ColumnEndpointsTrafficType = new DataGridViewComboBoxColumn();
            ColumnEndpointsValidateSSLCertificate = new DataGridViewCheckBoxColumn();
            ColumnEndpointsDescription = new DataGridViewTextBoxColumn();
            label8 = new Label();
            label9 = new Label();
            comboBoxConnectionPattern = new ComboBox();
            tabPageAdvanced = new TabPage();
            groupBox4 = new GroupBox();
            textBoxStickySessionCacheExpiration = new TextBox();
            textBoxAcceptBacklogSize = new TextBox();
            label15 = new Label();
            groupBoxLimits = new GroupBox();
            textBoxIdleTimeout = new TextBox();
            labelIdleTimeout = new Label();
            textBoxMaxDatagrams = new TextBox();
            labelMaxDatagrams = new Label();
            textBoxMaxConnectionsPerClient = new TextBox();
            labelMaxConnectionsPerClient = new Label();
            textBoxMaxConnections = new TextBox();
            labelMaxConnections = new Label();
            groupBox1 = new GroupBox();
            textBoxMaxBufferSize = new TextBox();
            textBoxInitialBufferSize = new TextBox();
            tabPagePlugins = new TabPage();
            labelPluginsInfo = new Label();
            checkedListBoxPlugins = new CheckedListBox();
            buttonCancel = new Button();
            buttonSave = new Button();
            tabControl.SuspendLayout();
            tabPageGeneral.SuspendLayout();
            tabPageBindings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewBindings).BeginInit();
            tabPageHostNames.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewHostNames).BeginInit();
            tabPageTls.SuspendLayout();
            tabPageHTTPHeaders.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewHTTPHeaders).BeginInit();
            panelForwardedHeaders.SuspendLayout();
            tabPageEndpoints.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewEndpoints).BeginInit();
            tabPageAdvanced.SuspendLayout();
            groupBox4.SuspendLayout();
            groupBoxLimits.SuspendLayout();
            groupBox1.SuspendLayout();
            tabPagePlugins.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(7, 3);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(39, 15);
            label1.TabIndex = 0;
            label1.Text = "Name";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(7, 49);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(66, 15);
            label2.TabIndex = 1;
            label2.Text = "Traffic type";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(8, 19);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(58, 15);
            label3.TabIndex = 2;
            label3.Text = "Initial size";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(7, 3);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(63, 15);
            label4.TabIndex = 3;
            label4.Text = "Listen port";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(8, 65);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(54, 15);
            label5.TabIndex = 4;
            label5.Text = "Max. size";
            // 
            // labelAcceptBacklogSize
            // 
            labelAcceptBacklogSize.AutoSize = true;
            labelAcceptBacklogSize.Location = new Point(8, 19);
            labelAcceptBacklogSize.Margin = new Padding(4, 0, 4, 0);
            labelAcceptBacklogSize.Name = "labelAcceptBacklogSize";
            labelAcceptBacklogSize.Size = new Size(111, 15);
            labelAcceptBacklogSize.TabIndex = 5;
            labelAcceptBacklogSize.Text = "Accept backlog size";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(7, 95);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(92, 15);
            label7.TabIndex = 6;
            label7.Text = "Listen addresses";
            // 
            // checkBoxListenOnAllAddresses
            // 
            checkBoxListenOnAllAddresses.AutoSize = true;
            checkBoxListenOnAllAddresses.Checked = true;
            checkBoxListenOnAllAddresses.CheckState = CheckState.Checked;
            checkBoxListenOnAllAddresses.Location = new Point(9, 292);
            checkBoxListenOnAllAddresses.Margin = new Padding(4, 3, 4, 3);
            checkBoxListenOnAllAddresses.Name = "checkBoxListenOnAllAddresses";
            checkBoxListenOnAllAddresses.Size = new Size(152, 19);
            checkBoxListenOnAllAddresses.TabIndex = 8;
            checkBoxListenOnAllAddresses.Text = "Listen on All Addresses?";
            checkBoxListenOnAllAddresses.UseVisualStyleBackColor = true;
            checkBoxListenOnAllAddresses.CheckedChanged += CheckBoxListenOnAllAddresses_CheckedChanged;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabPageGeneral);
            tabControl.Controls.Add(tabPageBindings);
            tabControl.Controls.Add(tabPageHostNames);
            tabControl.Controls.Add(tabPageTls);
            tabControl.Controls.Add(tabPageHTTPHeaders);
            tabControl.Controls.Add(tabPageEndpoints);
            tabControl.Controls.Add(tabPageAdvanced);
            tabControl.Controls.Add(tabPagePlugins);
            tabControl.Location = new Point(14, 14);
            tabControl.Margin = new Padding(0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(603, 356);
            tabControl.TabIndex = 10;
            // 
            // tabPageGeneral
            // 
            tabPageGeneral.BackColor = SystemColors.Control;
            tabPageGeneral.Controls.Add(textBoxDescription);
            tabPageGeneral.Controls.Add(label6);
            tabPageGeneral.Controls.Add(checkBoxAllowHttp2);
            tabPageGeneral.Controls.Add(checkBoxListenAutoStart);
            tabPageGeneral.Controls.Add(comboBoxTrafficType);
            tabPageGeneral.Controls.Add(textBoxProxyName);
            tabPageGeneral.Controls.Add(label1);
            tabPageGeneral.Controls.Add(label2);
            tabPageGeneral.Location = new Point(4, 24);
            tabPageGeneral.Name = "tabPageGeneral";
            tabPageGeneral.Padding = new Padding(3);
            tabPageGeneral.Size = new Size(595, 328);
            tabPageGeneral.TabIndex = 0;
            tabPageGeneral.Text = "General";
            // 
            // textBoxDescription
            // 
            textBoxDescription.AcceptsReturn = true;
            textBoxDescription.Location = new Point(9, 113);
            textBoxDescription.Margin = new Padding(4, 3, 4, 3);
            textBoxDescription.Multiline = true;
            textBoxDescription.Name = "textBoxDescription";
            textBoxDescription.ScrollBars = ScrollBars.Both;
            textBoxDescription.Size = new Size(579, 175);
            textBoxDescription.TabIndex = 4;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(9, 95);
            label6.Margin = new Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(67, 15);
            label6.TabIndex = 13;
            label6.Text = "Description";
            // 
            // checkBoxAllowHttp2
            // 
            checkBoxAllowHttp2.AutoSize = true;
            checkBoxAllowHttp2.Location = new Point(157, 69);
            checkBoxAllowHttp2.Margin = new Padding(4, 3, 4, 3);
            checkBoxAllowHttp2.Name = "checkBoxAllowHttp2";
            checkBoxAllowHttp2.Size = new Size(100, 19);
            checkBoxAllowHttp2.TabIndex = 4;
            checkBoxAllowHttp2.Text = "Allow HTTP/2";
            checkBoxAllowHttp2.UseVisualStyleBackColor = true;
            // 
            // checkBoxListenAutoStart
            // 
            checkBoxListenAutoStart.AutoSize = true;
            checkBoxListenAutoStart.Checked = true;
            checkBoxListenAutoStart.CheckState = CheckState.Checked;
            checkBoxListenAutoStart.Location = new Point(9, 294);
            checkBoxListenAutoStart.Margin = new Padding(4, 3, 4, 3);
            checkBoxListenAutoStart.Name = "checkBoxListenAutoStart";
            checkBoxListenAutoStart.Size = new Size(83, 19);
            checkBoxListenAutoStart.TabIndex = 3;
            checkBoxListenAutoStart.Text = "Auto start?";
            checkBoxListenAutoStart.UseVisualStyleBackColor = true;
            // 
            // comboBoxTrafficType
            // 
            comboBoxTrafficType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxTrafficType.FormattingEnabled = true;
            comboBoxTrafficType.Location = new Point(9, 67);
            comboBoxTrafficType.Margin = new Padding(4, 3, 4, 3);
            comboBoxTrafficType.Name = "comboBoxTrafficType";
            comboBoxTrafficType.Size = new Size(140, 23);
            comboBoxTrafficType.TabIndex = 2;
            comboBoxTrafficType.SelectedIndexChanged += ComboBoxTrafficType_SelectedIndexChanged;
            // 
            // textBoxProxyName
            // 
            textBoxProxyName.Location = new Point(9, 21);
            textBoxProxyName.Margin = new Padding(4, 3, 4, 3);
            textBoxProxyName.Name = "textBoxProxyName";
            textBoxProxyName.Size = new Size(579, 23);
            textBoxProxyName.TabIndex = 1;
            // 
            // tabPageBindings
            // 
            tabPageBindings.BackColor = SystemColors.Control;
            tabPageBindings.Controls.Add(comboBoxBindingProtocol);
            tabPageBindings.Controls.Add(label10);
            tabPageBindings.Controls.Add(dataGridViewBindings);
            tabPageBindings.Controls.Add(textBoxListenPort);
            tabPageBindings.Controls.Add(label4);
            tabPageBindings.Controls.Add(checkBoxListenOnAllAddresses);
            tabPageBindings.Controls.Add(label7);
            tabPageBindings.Location = new Point(4, 24);
            tabPageBindings.Name = "tabPageBindings";
            tabPageBindings.Padding = new Padding(3);
            tabPageBindings.Size = new Size(595, 328);
            tabPageBindings.TabIndex = 1;
            tabPageBindings.Text = "Bindings";
            // 
            // comboBoxBindingProtocol
            // 
            comboBoxBindingProtocol.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxBindingProtocol.FormattingEnabled = true;
            comboBoxBindingProtocol.Location = new Point(9, 67);
            comboBoxBindingProtocol.Margin = new Padding(4, 3, 4, 3);
            comboBoxBindingProtocol.Name = "comboBoxBindingProtocol";
            comboBoxBindingProtocol.Size = new Size(140, 23);
            comboBoxBindingProtocol.TabIndex = 6;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(7, 49);
            label10.Margin = new Padding(4, 0, 4, 0);
            label10.Name = "label10";
            label10.Size = new Size(52, 15);
            label10.TabIndex = 11;
            label10.Text = "Protocol";
            // 
            // dataGridViewBindings
            // 
            dataGridViewBindings.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewBindings.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewBindings.Columns.AddRange(new DataGridViewColumn[] { ColumnBindingsEnabled, ColumnBindingsIPAddress, ColumnBindingsDescription });
            dataGridViewBindings.Location = new Point(9, 113);
            dataGridViewBindings.Margin = new Padding(4, 3, 4, 3);
            dataGridViewBindings.Name = "dataGridViewBindings";
            dataGridViewBindings.Size = new Size(579, 173);
            dataGridViewBindings.TabIndex = 7;
            dataGridViewBindings.Click += DataGridViewBindings_Click;
            // 
            // ColumnBindingsEnabled
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ColumnBindingsEnabled.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnBindingsEnabled.Frozen = true;
            ColumnBindingsEnabled.HeaderText = "Enabled";
            ColumnBindingsEnabled.Name = "ColumnBindingsEnabled";
            ColumnBindingsEnabled.Width = 55;
            // 
            // ColumnBindingsIPAddress
            // 
            ColumnBindingsIPAddress.HeaderText = "IP Address";
            ColumnBindingsIPAddress.Name = "ColumnBindingsIPAddress";
            ColumnBindingsIPAddress.Width = 125;
            // 
            // ColumnBindingsDescription
            // 
            ColumnBindingsDescription.HeaderText = "Description";
            ColumnBindingsDescription.Name = "ColumnBindingsDescription";
            ColumnBindingsDescription.Width = 250;
            // 
            // textBoxListenPort
            // 
            textBoxListenPort.Location = new Point(9, 21);
            textBoxListenPort.Margin = new Padding(4, 3, 4, 3);
            textBoxListenPort.Name = "textBoxListenPort";
            textBoxListenPort.Size = new Size(140, 23);
            textBoxListenPort.TabIndex = 5;
            // 
            // tabPageHostNames
            // 
            tabPageHostNames.BackColor = SystemColors.Control;
            tabPageHostNames.Controls.Add(labelHostNamesInfo);
            tabPageHostNames.Controls.Add(dataGridViewHostNames);
            tabPageHostNames.Location = new Point(4, 24);
            tabPageHostNames.Name = "tabPageHostNames";
            tabPageHostNames.Padding = new Padding(3);
            tabPageHostNames.Size = new Size(595, 328);
            tabPageHostNames.TabIndex = 6;
            tabPageHostNames.Text = "Host names";
            // 
            // labelHostNamesInfo
            // 
            labelHostNamesInfo.Location = new Point(9, 9);
            labelHostNamesInfo.Name = "labelHostNamesInfo";
            labelHostNamesInfo.Size = new Size(579, 73);
            labelHostNamesInfo.TabIndex = 0;
            labelHostNamesInfo.Text = "Proxies can share a port if they have different host names. A request goes to the proxy that has its host name; a proxy with no host names gets the rest. Only HTTP and HTTPS proxies can share a port.";
            // 
            // dataGridViewHostNames
            // 
            dataGridViewHostNames.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewHostNames.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewHostNames.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewHostNames.Columns.AddRange(new DataGridViewColumn[] { ColumnHostNamesEnabled, ColumnHostNamesHost, ColumnHostNamesCertificate, ColumnHostNamesDescription });
            dataGridViewHostNames.Location = new Point(9, 85);
            dataGridViewHostNames.Margin = new Padding(4, 3, 4, 3);
            dataGridViewHostNames.Name = "dataGridViewHostNames";
            dataGridViewHostNames.Size = new Size(579, 234);
            dataGridViewHostNames.TabIndex = 1;
            dataGridViewHostNames.DataError += DataGridViewHostNames_DataError;
            // 
            // ColumnHostNamesEnabled
            // 
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.NullValue = true;
            ColumnHostNamesEnabled.DefaultCellStyle = dataGridViewCellStyle2;
            ColumnHostNamesEnabled.Frozen = true;
            ColumnHostNamesEnabled.HeaderText = "Enabled";
            ColumnHostNamesEnabled.Name = "ColumnHostNamesEnabled";
            ColumnHostNamesEnabled.Width = 55;
            // 
            // ColumnHostNamesHost
            // 
            ColumnHostNamesHost.HeaderText = "Host name";
            ColumnHostNamesHost.Name = "ColumnHostNamesHost";
            ColumnHostNamesHost.Width = 170;
            // 
            // ColumnHostNamesCertificate
            // 
            ColumnHostNamesCertificate.FlatStyle = FlatStyle.Flat;
            ColumnHostNamesCertificate.HeaderText = "Certificate";
            ColumnHostNamesCertificate.Name = "ColumnHostNamesCertificate";
            ColumnHostNamesCertificate.Width = 170;
            // 
            // ColumnHostNamesDescription
            // 
            ColumnHostNamesDescription.HeaderText = "Description";
            ColumnHostNamesDescription.Name = "ColumnHostNamesDescription";
            ColumnHostNamesDescription.Width = 120;
            // 
            // tabPageTls
            // 
            tabPageTls.BackColor = SystemColors.Control;
            tabPageTls.Controls.Add(labelTlsInfo);
            tabPageTls.Controls.Add(labelCertificate);
            tabPageTls.Controls.Add(comboBoxCertificate);
            tabPageTls.Location = new Point(4, 24);
            tabPageTls.Name = "tabPageTls";
            tabPageTls.Padding = new Padding(3);
            tabPageTls.Size = new Size(595, 328);
            tabPageTls.TabIndex = 5;
            tabPageTls.Text = "TLS";
            // 
            // labelTlsInfo
            // 
            labelTlsInfo.Location = new Point(14, 14);
            labelTlsInfo.Name = "labelTlsInfo";
            labelTlsInfo.Size = new Size(555, 62);
            labelTlsInfo.TabIndex = 0;
            labelTlsInfo.Text = resources.GetString("labelTlsInfo.Text");
            //
            // labelCertificate
            //
            labelCertificate.AutoSize = true;
            labelCertificate.Location = new Point(6, 94);
            labelCertificate.Name = "labelCertificate";
            labelCertificate.Size = new Size(133, 15);
            labelCertificate.TabIndex = 1;
            labelCertificate.Text = "Certificate of the proxy";
            //
            // comboBoxCertificate
            //
            comboBoxCertificate.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCertificate.FormattingEnabled = true;
            comboBoxCertificate.Location = new Point(8, 111);
            comboBoxCertificate.Name = "comboBoxCertificate";
            comboBoxCertificate.Size = new Size(300, 23);
            comboBoxCertificate.TabIndex = 2;
            // 
            // tabPageHTTPHeaders
            // 
            tabPageHTTPHeaders.BackColor = SystemColors.Control;
            tabPageHTTPHeaders.Controls.Add(dataGridViewHTTPHeaders);
            tabPageHTTPHeaders.Controls.Add(panelForwardedHeaders);
            tabPageHTTPHeaders.Location = new Point(4, 24);
            tabPageHTTPHeaders.Name = "tabPageHTTPHeaders";
            tabPageHTTPHeaders.Padding = new Padding(3);
            tabPageHTTPHeaders.Size = new Size(595, 328);
            tabPageHTTPHeaders.TabIndex = 2;
            tabPageHTTPHeaders.Text = "HTTP Headers";
            // 
            // dataGridViewHTTPHeaders
            // 
            dataGridViewHTTPHeaders.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewHTTPHeaders.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewHTTPHeaders.Columns.AddRange(new DataGridViewColumn[] { ColumnHTTPHeadersEnabled, ColumnHTTPHeadersType, ColumnHTTPHeadersVerb, ColumnHTTPHeadersHeader, ColumnHTTPHeadersAction, ColumnHTTPHeadersValue, ColumnHTTPHeadersDescription });
            dataGridViewHTTPHeaders.Dock = DockStyle.Fill;
            dataGridViewHTTPHeaders.Location = new Point(3, 3);
            dataGridViewHTTPHeaders.Margin = new Padding(4, 3, 4, 3);
            dataGridViewHTTPHeaders.Name = "dataGridViewHTTPHeaders";
            dataGridViewHTTPHeaders.Size = new Size(589, 288);
            dataGridViewHTTPHeaders.TabIndex = 9;
            dataGridViewHTTPHeaders.Click += DataGridViewHTTPHeaders_Click;
            // 
            // ColumnHTTPHeadersEnabled
            // 
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ColumnHTTPHeadersEnabled.DefaultCellStyle = dataGridViewCellStyle3;
            ColumnHTTPHeadersEnabled.HeaderText = "Enabled";
            ColumnHTTPHeadersEnabled.Name = "ColumnHTTPHeadersEnabled";
            ColumnHTTPHeadersEnabled.Width = 55;
            // 
            // ColumnHTTPHeadersType
            // 
            ColumnHTTPHeadersType.HeaderText = "Type";
            ColumnHTTPHeadersType.Items.AddRange(new object[] { "None", "Request", "Response", "Any" });
            ColumnHTTPHeadersType.Name = "ColumnHTTPHeadersType";
            ColumnHTTPHeadersType.Resizable = DataGridViewTriState.True;
            ColumnHTTPHeadersType.SortMode = DataGridViewColumnSortMode.Automatic;
            ColumnHTTPHeadersType.Width = 75;
            // 
            // ColumnHTTPHeadersVerb
            // 
            ColumnHTTPHeadersVerb.HeaderText = "Verb";
            ColumnHTTPHeadersVerb.Items.AddRange(new object[] { "Any", "Connect", "Delete", "Get", "Head", "Options", "Patch", "Post", "Put", "Trace" });
            ColumnHTTPHeadersVerb.Name = "ColumnHTTPHeadersVerb";
            ColumnHTTPHeadersVerb.Resizable = DataGridViewTriState.True;
            ColumnHTTPHeadersVerb.SortMode = DataGridViewColumnSortMode.Automatic;
            ColumnHTTPHeadersVerb.Width = 75;
            // 
            // ColumnHTTPHeadersHeader
            // 
            ColumnHTTPHeadersHeader.HeaderText = "Header";
            ColumnHTTPHeadersHeader.Name = "ColumnHTTPHeadersHeader";
            // 
            // ColumnHTTPHeadersAction
            // 
            ColumnHTTPHeadersAction.HeaderText = "Action";
            ColumnHTTPHeadersAction.Items.AddRange(new object[] { "Insert", "Update", "Delete", "Upsert" });
            ColumnHTTPHeadersAction.Name = "ColumnHTTPHeadersAction";
            ColumnHTTPHeadersAction.Resizable = DataGridViewTriState.True;
            ColumnHTTPHeadersAction.SortMode = DataGridViewColumnSortMode.Automatic;
            ColumnHTTPHeadersAction.Width = 75;
            // 
            // ColumnHTTPHeadersValue
            // 
            ColumnHTTPHeadersValue.HeaderText = "Value";
            ColumnHTTPHeadersValue.Name = "ColumnHTTPHeadersValue";
            // 
            // ColumnHTTPHeadersDescription
            // 
            ColumnHTTPHeadersDescription.HeaderText = "Description";
            ColumnHTTPHeadersDescription.Name = "ColumnHTTPHeadersDescription";
            ColumnHTTPHeadersDescription.Width = 250;
            // 
            // panelForwardedHeaders
            // 
            panelForwardedHeaders.Controls.Add(labelForwardedHeaders);
            panelForwardedHeaders.Controls.Add(comboBoxForwardedHeaders);
            panelForwardedHeaders.Dock = DockStyle.Bottom;
            panelForwardedHeaders.Location = new Point(3, 291);
            panelForwardedHeaders.Name = "panelForwardedHeaders";
            panelForwardedHeaders.Size = new Size(589, 34);
            panelForwardedHeaders.TabIndex = 10;
            // 
            // labelForwardedHeaders
            // 
            labelForwardedHeaders.AutoSize = true;
            labelForwardedHeaders.Location = new Point(6, 9);
            labelForwardedHeaders.Name = "labelForwardedHeaders";
            labelForwardedHeaders.Size = new Size(142, 15);
            labelForwardedHeaders.TabIndex = 0;
            labelForwardedHeaders.Text = "X-Forwarded-For / -Proto";
            // 
            // comboBoxForwardedHeaders
            // 
            comboBoxForwardedHeaders.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxForwardedHeaders.FormattingEnabled = true;
            comboBoxForwardedHeaders.Location = new Point(190, 5);
            comboBoxForwardedHeaders.Name = "comboBoxForwardedHeaders";
            comboBoxForwardedHeaders.Size = new Size(220, 23);
            comboBoxForwardedHeaders.TabIndex = 1;
            // 
            // tabPageEndpoints
            // 
            tabPageEndpoints.BackColor = SystemColors.Control;
            tabPageEndpoints.Controls.Add(checkBoxUseStickySessions);
            tabPageEndpoints.Controls.Add(dataGridViewEndpoints);
            tabPageEndpoints.Controls.Add(label8);
            tabPageEndpoints.Controls.Add(label9);
            tabPageEndpoints.Controls.Add(comboBoxConnectionPattern);
            tabPageEndpoints.Location = new Point(4, 24);
            tabPageEndpoints.Name = "tabPageEndpoints";
            tabPageEndpoints.Padding = new Padding(3);
            tabPageEndpoints.Size = new Size(595, 328);
            tabPageEndpoints.TabIndex = 4;
            tabPageEndpoints.Text = "Endpoints";
            // 
            // checkBoxUseStickySessions
            // 
            checkBoxUseStickySessions.AutoSize = true;
            checkBoxUseStickySessions.Location = new Point(224, 23);
            checkBoxUseStickySessions.Margin = new Padding(4, 3, 4, 3);
            checkBoxUseStickySessions.Name = "checkBoxUseStickySessions";
            checkBoxUseStickySessions.Size = new Size(129, 19);
            checkBoxUseStickySessions.TabIndex = 19;
            checkBoxUseStickySessions.Text = "Use sticky sessions?";
            checkBoxUseStickySessions.UseVisualStyleBackColor = true;
            // 
            // dataGridViewEndpoints
            // 
            dataGridViewEndpoints.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewEndpoints.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewEndpoints.Columns.AddRange(new DataGridViewColumn[] { ColumnEndpointsEnabled, ColumnEndpointsAddress, ColumnEndpointsPort, ColumnEndpointsTrafficType, ColumnEndpointsValidateSSLCertificate, ColumnEndpointsDescription });
            dataGridViewEndpoints.Location = new Point(9, 67);
            dataGridViewEndpoints.Margin = new Padding(4, 3, 4, 3);
            dataGridViewEndpoints.Name = "dataGridViewEndpoints";
            dataGridViewEndpoints.Size = new Size(579, 229);
            dataGridViewEndpoints.TabIndex = 14;
            // 
            // ColumnEndpointsEnabled
            // 
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ColumnEndpointsEnabled.DefaultCellStyle = dataGridViewCellStyle4;
            ColumnEndpointsEnabled.HeaderText = "Enabled";
            ColumnEndpointsEnabled.Name = "ColumnEndpointsEnabled";
            ColumnEndpointsEnabled.Width = 55;
            // 
            // ColumnEndpointsAddress
            // 
            ColumnEndpointsAddress.HeaderText = "Address";
            ColumnEndpointsAddress.Name = "ColumnEndpointsAddress";
            ColumnEndpointsAddress.Width = 130;
            // 
            // ColumnEndpointsPort
            // 
            ColumnEndpointsPort.HeaderText = "Port";
            ColumnEndpointsPort.Name = "ColumnEndpointsPort";
            ColumnEndpointsPort.Width = 50;
            // 
            // ColumnEndpointsTrafficType
            // 
            ColumnEndpointsTrafficType.HeaderText = "Traffic";
            ColumnEndpointsTrafficType.Items.AddRange(new object[] { "Tcp", "Http", "Https", "Udp" });
            ColumnEndpointsTrafficType.Name = "ColumnEndpointsTrafficType";
            ColumnEndpointsTrafficType.Resizable = DataGridViewTriState.True;
            ColumnEndpointsTrafficType.SortMode = DataGridViewColumnSortMode.Automatic;
            ColumnEndpointsTrafficType.Width = 65;
            // 
            // ColumnEndpointsValidateSSLCertificate
            // 
            dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ColumnEndpointsValidateSSLCertificate.DefaultCellStyle = dataGridViewCellStyle5;
            ColumnEndpointsValidateSSLCertificate.HeaderText = "Validate Certificate";
            ColumnEndpointsValidateSSLCertificate.Name = "ColumnEndpointsValidateSSLCertificate";
            ColumnEndpointsValidateSSLCertificate.ToolTipText = "Ignore errors when validating the SSL certificate.";
            ColumnEndpointsValidateSSLCertificate.Width = 120;
            // 
            // ColumnEndpointsDescription
            // 
            ColumnEndpointsDescription.HeaderText = "Description";
            ColumnEndpointsDescription.Name = "ColumnEndpointsDescription";
            ColumnEndpointsDescription.Width = 150;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(7, 49);
            label8.Margin = new Padding(4, 0, 4, 0);
            label8.Name = "label8";
            label8.Size = new Size(104, 15);
            label8.TabIndex = 13;
            label8.Text = "Remote endpoints";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(7, 3);
            label9.Margin = new Padding(4, 0, 4, 0);
            label9.Name = "label9";
            label9.Size = new Size(110, 15);
            label9.TabIndex = 12;
            label9.Text = "Connection pattern";
            // 
            // comboBoxConnectionPattern
            // 
            comboBoxConnectionPattern.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxConnectionPattern.FormattingEnabled = true;
            comboBoxConnectionPattern.Location = new Point(9, 21);
            comboBoxConnectionPattern.Margin = new Padding(4, 3, 4, 3);
            comboBoxConnectionPattern.Name = "comboBoxConnectionPattern";
            comboBoxConnectionPattern.Size = new Size(207, 23);
            comboBoxConnectionPattern.TabIndex = 13;
            // 
            // tabPageAdvanced
            // 
            tabPageAdvanced.BackColor = SystemColors.Control;
            tabPageAdvanced.Controls.Add(groupBox4);
            tabPageAdvanced.Controls.Add(groupBoxLimits);
            tabPageAdvanced.Controls.Add(groupBox1);
            tabPageAdvanced.Location = new Point(4, 24);
            tabPageAdvanced.Name = "tabPageAdvanced";
            tabPageAdvanced.Padding = new Padding(3);
            tabPageAdvanced.Size = new Size(595, 328);
            tabPageAdvanced.TabIndex = 3;
            tabPageAdvanced.Text = "Advanced";
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(textBoxStickySessionCacheExpiration);
            groupBox4.Controls.Add(textBoxAcceptBacklogSize);
            groupBox4.Controls.Add(label15);
            groupBox4.Controls.Add(labelAcceptBacklogSize);
            groupBox4.Location = new Point(7, 130);
            groupBox4.Margin = new Padding(4, 3, 4, 3);
            groupBox4.Name = "groupBox4";
            groupBox4.Padding = new Padding(4, 3, 4, 3);
            groupBox4.Size = new Size(252, 123);
            groupBox4.TabIndex = 18;
            groupBox4.TabStop = false;
            groupBox4.Text = "Misc. (super advanced)";
            // 
            // textBoxStickySessionCacheExpiration
            // 
            textBoxStickySessionCacheExpiration.Location = new Point(10, 83);
            textBoxStickySessionCacheExpiration.Margin = new Padding(4, 3, 4, 3);
            textBoxStickySessionCacheExpiration.Name = "textBoxStickySessionCacheExpiration";
            textBoxStickySessionCacheExpiration.Size = new Size(116, 23);
            textBoxStickySessionCacheExpiration.TabIndex = 17;
            // 
            // textBoxAcceptBacklogSize
            // 
            textBoxAcceptBacklogSize.Location = new Point(10, 37);
            textBoxAcceptBacklogSize.Margin = new Padding(4, 3, 4, 3);
            textBoxAcceptBacklogSize.Name = "textBoxAcceptBacklogSize";
            textBoxAcceptBacklogSize.Size = new Size(116, 23);
            textBoxAcceptBacklogSize.TabIndex = 12;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new Point(8, 65);
            label15.Margin = new Padding(4, 0, 4, 0);
            label15.Name = "label15";
            label15.Size = new Size(150, 15);
            label15.TabIndex = 16;
            label15.Text = "Sticky session expiration (s)";
            // 
            // groupBoxLimits
            // 
            groupBoxLimits.Controls.Add(textBoxIdleTimeout);
            groupBoxLimits.Controls.Add(labelIdleTimeout);
            groupBoxLimits.Controls.Add(textBoxMaxDatagrams);
            groupBoxLimits.Controls.Add(labelMaxDatagrams);
            groupBoxLimits.Controls.Add(textBoxMaxConnectionsPerClient);
            groupBoxLimits.Controls.Add(labelMaxConnectionsPerClient);
            groupBoxLimits.Controls.Add(textBoxMaxConnections);
            groupBoxLimits.Controls.Add(labelMaxConnections);
            groupBoxLimits.Location = new Point(268, 6);
            groupBoxLimits.Margin = new Padding(4, 3, 4, 3);
            groupBoxLimits.Name = "groupBoxLimits";
            groupBoxLimits.Padding = new Padding(4, 3, 4, 3);
            groupBoxLimits.Size = new Size(252, 212);
            groupBoxLimits.TabIndex = 1;
            groupBoxLimits.TabStop = false;
            groupBoxLimits.Text = "Limits (0 is no limit)";
            // 
            // textBoxIdleTimeout
            // 
            textBoxIdleTimeout.Location = new Point(10, 129);
            textBoxIdleTimeout.Margin = new Padding(4, 3, 4, 3);
            textBoxIdleTimeout.Name = "textBoxIdleTimeout";
            textBoxIdleTimeout.Size = new Size(116, 23);
            textBoxIdleTimeout.TabIndex = 25;
            // 
            // labelIdleTimeout
            // 
            labelIdleTimeout.AutoSize = true;
            labelIdleTimeout.Location = new Point(8, 111);
            labelIdleTimeout.Margin = new Padding(4, 0, 4, 0);
            labelIdleTimeout.Name = "labelIdleTimeout";
            labelIdleTimeout.Size = new Size(87, 15);
            labelIdleTimeout.TabIndex = 24;
            labelIdleTimeout.Text = "Idle timeout (s)";
            // 
            // textBoxMaxDatagrams
            // 
            textBoxMaxDatagrams.Location = new Point(10, 175);
            textBoxMaxDatagrams.Margin = new Padding(4, 3, 4, 3);
            textBoxMaxDatagrams.Name = "textBoxMaxDatagrams";
            textBoxMaxDatagrams.Size = new Size(116, 23);
            textBoxMaxDatagrams.TabIndex = 27;
            // 
            // labelMaxDatagrams
            // 
            labelMaxDatagrams.AutoSize = true;
            labelMaxDatagrams.Location = new Point(8, 157);
            labelMaxDatagrams.Margin = new Padding(4, 0, 4, 0);
            labelMaxDatagrams.Name = "labelMaxDatagrams";
            labelMaxDatagrams.Size = new Size(184, 15);
            labelMaxDatagrams.TabIndex = 26;
            labelMaxDatagrams.Text = "Max datagrams/s per client (UDP)";
            // 
            // textBoxMaxConnectionsPerClient
            // 
            textBoxMaxConnectionsPerClient.Location = new Point(10, 83);
            textBoxMaxConnectionsPerClient.Margin = new Padding(4, 3, 4, 3);
            textBoxMaxConnectionsPerClient.Name = "textBoxMaxConnectionsPerClient";
            textBoxMaxConnectionsPerClient.Size = new Size(116, 23);
            textBoxMaxConnectionsPerClient.TabIndex = 23;
            // 
            // labelMaxConnectionsPerClient
            // 
            labelMaxConnectionsPerClient.AutoSize = true;
            labelMaxConnectionsPerClient.Location = new Point(8, 65);
            labelMaxConnectionsPerClient.Margin = new Padding(4, 0, 4, 0);
            labelMaxConnectionsPerClient.Name = "labelMaxConnectionsPerClient";
            labelMaxConnectionsPerClient.Size = new Size(152, 15);
            labelMaxConnectionsPerClient.TabIndex = 22;
            labelMaxConnectionsPerClient.Text = "Max. connections per client";
            // 
            // textBoxMaxConnections
            // 
            textBoxMaxConnections.Location = new Point(10, 37);
            textBoxMaxConnections.Margin = new Padding(4, 3, 4, 3);
            textBoxMaxConnections.Name = "textBoxMaxConnections";
            textBoxMaxConnections.Size = new Size(116, 23);
            textBoxMaxConnections.TabIndex = 21;
            // 
            // labelMaxConnections
            // 
            labelMaxConnections.AutoSize = true;
            labelMaxConnections.Location = new Point(8, 19);
            labelMaxConnections.Margin = new Padding(4, 0, 4, 0);
            labelMaxConnections.Name = "labelMaxConnections";
            labelMaxConnections.Size = new Size(100, 15);
            labelMaxConnections.TabIndex = 20;
            labelMaxConnections.Text = "Max. connections";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(textBoxMaxBufferSize);
            groupBox1.Controls.Add(label5);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(textBoxInitialBufferSize);
            groupBox1.Location = new Point(7, 6);
            groupBox1.Margin = new Padding(4, 3, 4, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4, 3, 4, 3);
            groupBox1.Size = new Size(252, 118);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Buffers";
            // 
            // textBoxMaxBufferSize
            // 
            textBoxMaxBufferSize.Location = new Point(10, 83);
            textBoxMaxBufferSize.Margin = new Padding(4, 3, 4, 3);
            textBoxMaxBufferSize.Name = "textBoxMaxBufferSize";
            textBoxMaxBufferSize.Size = new Size(116, 23);
            textBoxMaxBufferSize.TabIndex = 11;
            // 
            // textBoxInitialBufferSize
            // 
            textBoxInitialBufferSize.Location = new Point(10, 37);
            textBoxInitialBufferSize.Margin = new Padding(4, 3, 4, 3);
            textBoxInitialBufferSize.Name = "textBoxInitialBufferSize";
            textBoxInitialBufferSize.Size = new Size(116, 23);
            textBoxInitialBufferSize.TabIndex = 10;
            // 
            // tabPagePlugins
            // 
            tabPagePlugins.BackColor = SystemColors.Control;
            tabPagePlugins.Controls.Add(labelPluginsInfo);
            tabPagePlugins.Controls.Add(checkedListBoxPlugins);
            tabPagePlugins.Location = new Point(4, 24);
            tabPagePlugins.Name = "tabPagePlugins";
            tabPagePlugins.Padding = new Padding(3);
            tabPagePlugins.Size = new Size(595, 328);
            tabPagePlugins.TabIndex = 7;
            tabPagePlugins.Text = "Plugins";
            // 
            // labelPluginsInfo
            // 
            labelPluginsInfo.Location = new Point(9, 9);
            labelPluginsInfo.Name = "labelPluginsInfo";
            labelPluginsInfo.Size = new Size(579, 40);
            labelPluginsInfo.TabIndex = 0;
            labelPluginsInfo.Text = "Tick the plugins that this proxy uses. They are set up for the whole service (Settings, Plugins) and are called in the order shown. A plugin that is not ticked is not called for this proxy.";
            // 
            // checkedListBoxPlugins
            // 
            checkedListBoxPlugins.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            checkedListBoxPlugins.CheckOnClick = true;
            checkedListBoxPlugins.FormattingEnabled = true;
            checkedListBoxPlugins.IntegralHeight = false;
            checkedListBoxPlugins.Location = new Point(9, 85);
            checkedListBoxPlugins.Name = "checkedListBoxPlugins";
            checkedListBoxPlugins.Size = new Size(579, 234);
            checkedListBoxPlugins.TabIndex = 1;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(506, 373);
            buttonCancel.Margin = new Padding(4, 3, 4, 3);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(104, 27);
            buttonCancel.TabIndex = 25;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // buttonSave
            // 
            buttonSave.Location = new Point(394, 373);
            buttonSave.Margin = new Padding(4, 3, 4, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(104, 27);
            buttonSave.TabIndex = 24;
            buttonSave.Text = "Save";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // FormProxy
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(623, 410);
            Controls.Add(buttonSave);
            Controls.Add(buttonCancel);
            Controls.Add(tabControl);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormProxy";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Proxy";
            FormClosed += FormProxy_FormClosed;
            Shown += FormProxy_Shown;
            tabControl.ResumeLayout(false);
            tabPageGeneral.ResumeLayout(false);
            tabPageGeneral.PerformLayout();
            tabPageBindings.ResumeLayout(false);
            tabPageBindings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewBindings).EndInit();
            tabPageHostNames.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewHostNames).EndInit();
            tabPageTls.ResumeLayout(false);
            tabPageTls.PerformLayout();
            tabPageHTTPHeaders.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewHTTPHeaders).EndInit();
            panelForwardedHeaders.ResumeLayout(false);
            panelForwardedHeaders.PerformLayout();
            tabPageEndpoints.ResumeLayout(false);
            tabPageEndpoints.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewEndpoints).EndInit();
            tabPageAdvanced.ResumeLayout(false);
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            groupBoxLimits.ResumeLayout(false);
            groupBoxLimits.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            tabPagePlugins.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        private Label labelAcceptBacklogSize;
        private Label label7;
        private CheckBox checkBoxListenOnAllAddresses;
        private TabControl tabControl;
        private TabPage tabPageGeneral;
        private ComboBox comboBoxTrafficType;
        private TextBox textBoxProxyName;
        private TabPage tabPageBindings;
        private TabPage tabPageHostNames;
        private Label labelHostNamesInfo;
        private DataGridView dataGridViewHostNames;
        private TabPage tabPagePlugins;
        private Label labelPluginsInfo;
        private CheckedListBox checkedListBoxPlugins;
        private Panel panelForwardedHeaders;
        private Label labelForwardedHeaders;
        private ComboBox comboBoxForwardedHeaders;
        private Label labelMaxDatagrams;
        private TextBox textBoxMaxDatagrams;
        private DataGridViewCheckBoxColumn ColumnHostNamesEnabled;
        private DataGridViewTextBoxColumn ColumnHostNamesHost;
        private DataGridViewComboBoxColumn ColumnHostNamesCertificate;
        private DataGridViewTextBoxColumn ColumnHostNamesDescription;
        private TextBox textBoxListenPort;
        private TabPage tabPageHTTPHeaders;
        private TabPage tabPageAdvanced;
        private GroupBox groupBox1;
        private TextBox textBoxMaxBufferSize;
        private TextBox textBoxInitialBufferSize;
        private TabPage tabPageEndpoints;
        private TextBox textBoxAcceptBacklogSize;
        private Label label8;
        private Label label9;
        private ComboBox comboBoxConnectionPattern;
        private Button buttonCancel;
        private Button buttonSave;
        private DataGridView dataGridViewEndpoints;
        private DataGridView dataGridViewBindings;
        private DataGridView dataGridViewHTTPHeaders;
        private CheckBox checkBoxListenAutoStart;
        private TextBox textBoxDescription;
        private Label label6;
        private ComboBox comboBoxBindingProtocol;
        private Label label10;
        private CheckBox checkBoxUseStickySessions;
        private TextBox textBoxStickySessionCacheExpiration;
        private Label label15;
        private GroupBox groupBox4;
        private GroupBox groupBoxLimits;
        private Label labelMaxConnections;
        private TextBox textBoxMaxConnections;
        private Label labelMaxConnectionsPerClient;
        private TextBox textBoxMaxConnectionsPerClient;
        private Label labelIdleTimeout;
        private TextBox textBoxIdleTimeout;
        private CheckBox checkBoxAllowHttp2;
        private DataGridViewCheckBoxColumn ColumnHTTPHeadersEnabled;
        private DataGridViewComboBoxColumn ColumnHTTPHeadersType;
        private DataGridViewComboBoxColumn ColumnHTTPHeadersVerb;
        private DataGridViewTextBoxColumn ColumnHTTPHeadersHeader;
        private DataGridViewComboBoxColumn ColumnHTTPHeadersAction;
        private DataGridViewTextBoxColumn ColumnHTTPHeadersValue;
        private DataGridViewTextBoxColumn ColumnHTTPHeadersDescription;
        private TabPage tabPageTls;
        private Label labelTlsInfo;
        private Label labelCertificate;
        private ComboBox comboBoxCertificate;
        private DataGridViewCheckBoxColumn ColumnBindingsEnabled;
        private DataGridViewTextBoxColumn ColumnBindingsIPAddress;
        private DataGridViewTextBoxColumn ColumnBindingsDescription;
        private DataGridViewCheckBoxColumn ColumnEndpointsEnabled;
        private DataGridViewTextBoxColumn ColumnEndpointsAddress;
        private DataGridViewTextBoxColumn ColumnEndpointsPort;
        private DataGridViewComboBoxColumn ColumnEndpointsTrafficType;
        private DataGridViewCheckBoxColumn ColumnEndpointsValidateSSLCertificate;
        private DataGridViewTextBoxColumn ColumnEndpointsDescription;
    }
}