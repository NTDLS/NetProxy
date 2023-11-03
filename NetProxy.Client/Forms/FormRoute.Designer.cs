namespace NetProxy.Client.Forms
{
    partial class FormRoute
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormRoute));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.labelAcceptBacklogSize = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.checkBoxListenOnAllAddresses = new System.Windows.Forms.CheckBox();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageGeneral = new System.Windows.Forms.TabPage();
            this.textBoxDescription = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.checkBoxListenAutoStart = new System.Windows.Forms.CheckBox();
            this.comboBoxTrafficType = new System.Windows.Forms.ComboBox();
            this.textBoxRouteName = new System.Windows.Forms.TextBox();
            this.tabPageBindings = new System.Windows.Forms.TabPage();
            this.comboBoxBindingProtocol = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.dataGridViewBindings = new System.Windows.Forms.DataGridView();
            this.ColumnBindingsEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ColumnBindingsIPAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnBindingsDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.textBoxListenPort = new System.Windows.Forms.TextBox();
            this.tabPageHTTPHeaders = new System.Windows.Forms.TabPage();
            this.dataGridViewHTTPHeaders = new System.Windows.Forms.DataGridView();
            this.ColumnHTTPHeadersEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ColumnHTTPHeadersType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnHTTPHeadersVerb = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnHTTPHeadersHeader = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnHTTPHeadersAction = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnHTTPHeadersValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnHTTPHeadersDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabPageEndpoints = new System.Windows.Forms.TabPage();
            this.checkBoxUseStickySessions = new System.Windows.Forms.CheckBox();
            this.dataGridViewEndpoints = new System.Windows.Forms.DataGridView();
            this.ColumnEndpointsEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ColumnEndpointsAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnEndpointsPort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnEndpointsDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.comboBoxConnectionPattern = new System.Windows.Forms.ComboBox();
            this.tabPageTunneling = new System.Windows.Forms.TabPage();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label11 = new System.Windows.Forms.Label();
            this.checkBoxTunnelEndpointUseEncryption = new System.Windows.Forms.CheckBox();
            this.checkBoxEndpointIsTunnel = new System.Windows.Forms.CheckBox();
            this.textBoxTunnelEndpointPreSharedKey = new System.Windows.Forms.TextBox();
            this.checkBoxTunnelEndpointUseCompression = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label12 = new System.Windows.Forms.Label();
            this.checkBoxTunnelBindingUseEncryption = new System.Windows.Forms.CheckBox();
            this.textBoxTunnelBindingPreSharedKey = new System.Windows.Forms.TextBox();
            this.checkBoxTunnelBindingUseCompression = new System.Windows.Forms.CheckBox();
            this.checkBoxBindingIsTunnel = new System.Windows.Forms.CheckBox();
            this.tabPageAdvanced = new System.Windows.Forms.TabPage();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.textBoxEncryptionInitTimeout = new System.Windows.Forms.TextBox();
            this.textBoxStickySessionCacheExpiration = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.textBoxSpinLockCount = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textBoxMaxBufferSize = new System.Windows.Forms.TextBox();
            this.textBoxAcceptBacklogSize = new System.Windows.Forms.TextBox();
            this.textBoxInitialBufferSize = new System.Windows.Forms.TextBox();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tabControl.SuspendLayout();
            this.tabPageGeneral.SuspendLayout();
            this.tabPageBindings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewBindings)).BeginInit();
            this.tabPageHTTPHeaders.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewHTTPHeaders)).BeginInit();
            this.tabPageEndpoints.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEndpoints)).BeginInit();
            this.tabPageTunneling.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.tabPageAdvanced.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(32, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Name";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Traffic Type";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(55, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Initial Size";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(36, 16);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(57, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Listen Port";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(59, 51);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(50, 13);
            this.label5.TabIndex = 4;
            this.label5.Text = "Max Size";
            // 
            // labelAcceptBacklogSize
            // 
            this.labelAcceptBacklogSize.AutoSize = true;
            this.labelAcceptBacklogSize.Location = new System.Drawing.Point(3, 77);
            this.labelAcceptBacklogSize.Name = "labelAcceptBacklogSize";
            this.labelAcceptBacklogSize.Size = new System.Drawing.Size(106, 13);
            this.labelAcceptBacklogSize.TabIndex = 5;
            this.labelAcceptBacklogSize.Text = "Accept Backlog Size";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(9, 75);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(87, 13);
            this.label7.TabIndex = 6;
            this.label7.Text = "Listen Addresses";
            // 
            // checkBoxListenOnAllAddresses
            // 
            this.checkBoxListenOnAllAddresses.AutoSize = true;
            this.checkBoxListenOnAllAddresses.Location = new System.Drawing.Point(12, 247);
            this.checkBoxListenOnAllAddresses.Name = "checkBoxListenOnAllAddresses";
            this.checkBoxListenOnAllAddresses.Size = new System.Drawing.Size(141, 17);
            this.checkBoxListenOnAllAddresses.TabIndex = 8;
            this.checkBoxListenOnAllAddresses.Text = "Listen on All Addresses?";
            this.checkBoxListenOnAllAddresses.UseVisualStyleBackColor = true;
            this.checkBoxListenOnAllAddresses.CheckedChanged += new System.EventHandler(this.checkBoxListenOnAllAddresses_CheckedChanged);
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPageGeneral);
            this.tabControl.Controls.Add(this.tabPageBindings);
            this.tabControl.Controls.Add(this.tabPageHTTPHeaders);
            this.tabControl.Controls.Add(this.tabPageEndpoints);
            this.tabControl.Controls.Add(this.tabPageTunneling);
            this.tabControl.Controls.Add(this.tabPageAdvanced);
            this.tabControl.Location = new System.Drawing.Point(12, 12);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(591, 298);
            this.tabControl.TabIndex = 10;
            // 
            // tabPageGeneral
            // 
            this.tabPageGeneral.Controls.Add(this.textBoxDescription);
            this.tabPageGeneral.Controls.Add(this.label6);
            this.tabPageGeneral.Controls.Add(this.checkBoxListenAutoStart);
            this.tabPageGeneral.Controls.Add(this.comboBoxTrafficType);
            this.tabPageGeneral.Controls.Add(this.textBoxRouteName);
            this.tabPageGeneral.Controls.Add(this.label1);
            this.tabPageGeneral.Controls.Add(this.label2);
            this.tabPageGeneral.Location = new System.Drawing.Point(4, 22);
            this.tabPageGeneral.Name = "tabPageGeneral";
            this.tabPageGeneral.Size = new System.Drawing.Size(583, 272);
            this.tabPageGeneral.TabIndex = 0;
            this.tabPageGeneral.Text = "General";
            this.tabPageGeneral.UseVisualStyleBackColor = true;
            // 
            // textBoxDescription
            // 
            this.textBoxDescription.AcceptsReturn = true;
            this.textBoxDescription.Location = new System.Drawing.Point(73, 99);
            this.textBoxDescription.Multiline = true;
            this.textBoxDescription.Name = "textBoxDescription";
            this.textBoxDescription.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBoxDescription.Size = new System.Drawing.Size(495, 152);
            this.textBoxDescription.TabIndex = 4;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(7, 99);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(60, 13);
            this.label6.TabIndex = 13;
            this.label6.Text = "Description";
            // 
            // checkBoxListenAutoStart
            // 
            this.checkBoxListenAutoStart.AutoSize = true;
            this.checkBoxListenAutoStart.Location = new System.Drawing.Point(73, 64);
            this.checkBoxListenAutoStart.Name = "checkBoxListenAutoStart";
            this.checkBoxListenAutoStart.Size = new System.Drawing.Size(79, 17);
            this.checkBoxListenAutoStart.TabIndex = 3;
            this.checkBoxListenAutoStart.Text = "Auto Start?";
            this.checkBoxListenAutoStart.UseVisualStyleBackColor = true;
            // 
            // comboBoxTrafficType
            // 
            this.comboBoxTrafficType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTrafficType.FormattingEnabled = true;
            this.comboBoxTrafficType.Location = new System.Drawing.Point(73, 37);
            this.comboBoxTrafficType.Name = "comboBoxTrafficType";
            this.comboBoxTrafficType.Size = new System.Drawing.Size(121, 21);
            this.comboBoxTrafficType.TabIndex = 2;
            this.comboBoxTrafficType.SelectedIndexChanged += new System.EventHandler(this.comboBoxTrafficType_SelectedIndexChanged);
            // 
            // textBoxRouteName
            // 
            this.textBoxRouteName.Location = new System.Drawing.Point(73, 11);
            this.textBoxRouteName.Name = "textBoxRouteName";
            this.textBoxRouteName.Size = new System.Drawing.Size(495, 20);
            this.textBoxRouteName.TabIndex = 1;
            // 
            // tabPageBindings
            // 
            this.tabPageBindings.Controls.Add(this.comboBoxBindingProtocol);
            this.tabPageBindings.Controls.Add(this.label10);
            this.tabPageBindings.Controls.Add(this.dataGridViewBindings);
            this.tabPageBindings.Controls.Add(this.textBoxListenPort);
            this.tabPageBindings.Controls.Add(this.label4);
            this.tabPageBindings.Controls.Add(this.checkBoxListenOnAllAddresses);
            this.tabPageBindings.Controls.Add(this.label7);
            this.tabPageBindings.Location = new System.Drawing.Point(4, 22);
            this.tabPageBindings.Name = "tabPageBindings";
            this.tabPageBindings.Size = new System.Drawing.Size(583, 272);
            this.tabPageBindings.TabIndex = 1;
            this.tabPageBindings.Text = "Bindings";
            this.tabPageBindings.UseVisualStyleBackColor = true;
            // 
            // comboBoxBindingProtocol
            // 
            this.comboBoxBindingProtocol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxBindingProtocol.FormattingEnabled = true;
            this.comboBoxBindingProtocol.Location = new System.Drawing.Point(99, 39);
            this.comboBoxBindingProtocol.Name = "comboBoxBindingProtocol";
            this.comboBoxBindingProtocol.Size = new System.Drawing.Size(121, 21);
            this.comboBoxBindingProtocol.TabIndex = 6;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(9, 43);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(84, 13);
            this.label10.TabIndex = 11;
            this.label10.Text = "Binding Protocol";
            // 
            // dataGridViewBindings
            // 
            this.dataGridViewBindings.BackgroundColor = System.Drawing.SystemColors.ButtonFace;
            this.dataGridViewBindings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewBindings.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnBindingsEnabled,
            this.ColumnBindingsIPAddress,
            this.ColumnBindingsDescription});
            this.dataGridViewBindings.Location = new System.Drawing.Point(12, 91);
            this.dataGridViewBindings.Name = "dataGridViewBindings";
            this.dataGridViewBindings.Size = new System.Drawing.Size(551, 150);
            this.dataGridViewBindings.TabIndex = 7;
            this.dataGridViewBindings.Click += new System.EventHandler(this.dataGridViewBindings_Click);
            // 
            // ColumnBindingsEnabled
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ColumnBindingsEnabled.DefaultCellStyle = dataGridViewCellStyle1;
            this.ColumnBindingsEnabled.Frozen = true;
            this.ColumnBindingsEnabled.HeaderText = "Enabled";
            this.ColumnBindingsEnabled.Name = "ColumnBindingsEnabled";
            this.ColumnBindingsEnabled.Width = 50;
            // 
            // ColumnBindingsIPAddress
            // 
            this.ColumnBindingsIPAddress.HeaderText = "IP Address";
            this.ColumnBindingsIPAddress.Name = "ColumnBindingsIPAddress";
            // 
            // ColumnBindingsDescription
            // 
            this.ColumnBindingsDescription.HeaderText = "Description";
            this.ColumnBindingsDescription.Name = "ColumnBindingsDescription";
            this.ColumnBindingsDescription.Width = 250;
            // 
            // textBoxListenPort
            // 
            this.textBoxListenPort.Location = new System.Drawing.Point(99, 13);
            this.textBoxListenPort.Name = "textBoxListenPort";
            this.textBoxListenPort.Size = new System.Drawing.Size(121, 20);
            this.textBoxListenPort.TabIndex = 5;
            // 
            // tabPageHTTPHeaders
            // 
            this.tabPageHTTPHeaders.Controls.Add(this.dataGridViewHTTPHeaders);
            this.tabPageHTTPHeaders.Location = new System.Drawing.Point(4, 22);
            this.tabPageHTTPHeaders.Name = "tabPageHTTPHeaders";
            this.tabPageHTTPHeaders.Size = new System.Drawing.Size(583, 272);
            this.tabPageHTTPHeaders.TabIndex = 2;
            this.tabPageHTTPHeaders.Text = "HTTPHeaders";
            this.tabPageHTTPHeaders.UseVisualStyleBackColor = true;
            // 
            // dataGridViewHTTPHeaders
            // 
            this.dataGridViewHTTPHeaders.BackgroundColor = System.Drawing.SystemColors.ButtonFace;
            this.dataGridViewHTTPHeaders.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewHTTPHeaders.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnHTTPHeadersEnabled,
            this.ColumnHTTPHeadersType,
            this.ColumnHTTPHeadersVerb,
            this.ColumnHTTPHeadersHeader,
            this.ColumnHTTPHeadersAction,
            this.ColumnHTTPHeadersValue,
            this.ColumnHTTPHeadersDescription});
            this.dataGridViewHTTPHeaders.Location = new System.Drawing.Point(7, 6);
            this.dataGridViewHTTPHeaders.Name = "dataGridViewHTTPHeaders";
            this.dataGridViewHTTPHeaders.Size = new System.Drawing.Size(569, 256);
            this.dataGridViewHTTPHeaders.TabIndex = 9;
            this.dataGridViewHTTPHeaders.Click += new System.EventHandler(this.dataGridViewHTTPHeaders_Click);
            // 
            // ColumnHTTPHeadersEnabled
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ColumnHTTPHeadersEnabled.DefaultCellStyle = dataGridViewCellStyle2;
            this.ColumnHTTPHeadersEnabled.HeaderText = "Enabled";
            this.ColumnHTTPHeadersEnabled.Name = "ColumnHTTPHeadersEnabled";
            this.ColumnHTTPHeadersEnabled.Width = 50;
            // 
            // ColumnHTTPHeadersType
            // 
            this.ColumnHTTPHeadersType.HeaderText = "Type";
            this.ColumnHTTPHeadersType.Items.AddRange(new object[] {
            "None",
            "Request",
            "Response",
            "Any"});
            this.ColumnHTTPHeadersType.Name = "ColumnHTTPHeadersType";
            this.ColumnHTTPHeadersType.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnHTTPHeadersType.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.ColumnHTTPHeadersType.Width = 75;
            // 
            // ColumnHTTPHeadersVerb
            // 
            this.ColumnHTTPHeadersVerb.HeaderText = "Verb";
            this.ColumnHTTPHeadersVerb.Items.AddRange(new object[] {
            "Any",
            "Connect",
            "Delete",
            "Get",
            "Head",
            "Options",
            "Post",
            "Put"});
            this.ColumnHTTPHeadersVerb.Name = "ColumnHTTPHeadersVerb";
            this.ColumnHTTPHeadersVerb.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnHTTPHeadersVerb.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.ColumnHTTPHeadersVerb.Width = 75;
            // 
            // ColumnHTTPHeadersHeader
            // 
            this.ColumnHTTPHeadersHeader.HeaderText = "Header";
            this.ColumnHTTPHeadersHeader.Name = "ColumnHTTPHeadersHeader";
            // 
            // ColumnHTTPHeadersAction
            // 
            this.ColumnHTTPHeadersAction.HeaderText = "Action";
            this.ColumnHTTPHeadersAction.Items.AddRange(new object[] {
            "Insert",
            "Update",
            "Delete",
            "Upsert"});
            this.ColumnHTTPHeadersAction.Name = "ColumnHTTPHeadersAction";
            this.ColumnHTTPHeadersAction.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnHTTPHeadersAction.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.ColumnHTTPHeadersAction.Width = 75;
            // 
            // ColumnHTTPHeadersValue
            // 
            this.ColumnHTTPHeadersValue.HeaderText = "Value";
            this.ColumnHTTPHeadersValue.Name = "ColumnHTTPHeadersValue";
            // 
            // ColumnHTTPHeadersDescription
            // 
            this.ColumnHTTPHeadersDescription.HeaderText = "Description";
            this.ColumnHTTPHeadersDescription.Name = "ColumnHTTPHeadersDescription";
            this.ColumnHTTPHeadersDescription.Width = 250;
            // 
            // tabPageEndpoints
            // 
            this.tabPageEndpoints.Controls.Add(this.checkBoxUseStickySessions);
            this.tabPageEndpoints.Controls.Add(this.dataGridViewEndpoints);
            this.tabPageEndpoints.Controls.Add(this.label8);
            this.tabPageEndpoints.Controls.Add(this.label9);
            this.tabPageEndpoints.Controls.Add(this.comboBoxConnectionPattern);
            this.tabPageEndpoints.Location = new System.Drawing.Point(4, 22);
            this.tabPageEndpoints.Name = "tabPageEndpoints";
            this.tabPageEndpoints.Size = new System.Drawing.Size(583, 272);
            this.tabPageEndpoints.TabIndex = 4;
            this.tabPageEndpoints.Text = "Endpoints";
            this.tabPageEndpoints.UseVisualStyleBackColor = true;
            // 
            // checkBoxUseStickySessions
            // 
            this.checkBoxUseStickySessions.AutoSize = true;
            this.checkBoxUseStickySessions.Location = new System.Drawing.Point(242, 11);
            this.checkBoxUseStickySessions.Name = "checkBoxUseStickySessions";
            this.checkBoxUseStickySessions.Size = new System.Drawing.Size(128, 17);
            this.checkBoxUseStickySessions.TabIndex = 19;
            this.checkBoxUseStickySessions.Text = "Use Sticky Sessions?";
            this.checkBoxUseStickySessions.UseVisualStyleBackColor = true;
            // 
            // dataGridViewEndpoints
            // 
            this.dataGridViewEndpoints.BackgroundColor = System.Drawing.SystemColors.ButtonFace;
            this.dataGridViewEndpoints.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewEndpoints.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnEndpointsEnabled,
            this.ColumnEndpointsAddress,
            this.ColumnEndpointsPort,
            this.ColumnEndpointsDescription});
            this.dataGridViewEndpoints.Location = new System.Drawing.Point(14, 49);
            this.dataGridViewEndpoints.Name = "dataGridViewEndpoints";
            this.dataGridViewEndpoints.Size = new System.Drawing.Size(553, 206);
            this.dataGridViewEndpoints.TabIndex = 14;
            // 
            // ColumnEndpointsEnabled
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ColumnEndpointsEnabled.DefaultCellStyle = dataGridViewCellStyle3;
            this.ColumnEndpointsEnabled.HeaderText = "Enabled";
            this.ColumnEndpointsEnabled.Name = "ColumnEndpointsEnabled";
            this.ColumnEndpointsEnabled.Width = 50;
            // 
            // ColumnEndpointsAddress
            // 
            this.ColumnEndpointsAddress.HeaderText = "Address";
            this.ColumnEndpointsAddress.Name = "ColumnEndpointsAddress";
            this.ColumnEndpointsAddress.Width = 150;
            // 
            // ColumnEndpointsPort
            // 
            this.ColumnEndpointsPort.HeaderText = "Port";
            this.ColumnEndpointsPort.Name = "ColumnEndpointsPort";
            this.ColumnEndpointsPort.Width = 50;
            // 
            // ColumnEndpointsDescription
            // 
            this.ColumnEndpointsDescription.HeaderText = "Description";
            this.ColumnEndpointsDescription.Name = "ColumnEndpointsDescription";
            this.ColumnEndpointsDescription.Width = 250;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(11, 33);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(94, 13);
            this.label8.TabIndex = 13;
            this.label8.Text = "Remote Endpoints";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(11, 12);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(98, 13);
            this.label9.TabIndex = 12;
            this.label9.Text = "Connection Pattern";
            // 
            // comboBoxConnectionPattern
            // 
            this.comboBoxConnectionPattern.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxConnectionPattern.FormattingEnabled = true;
            this.comboBoxConnectionPattern.Location = new System.Drawing.Point(115, 9);
            this.comboBoxConnectionPattern.Name = "comboBoxConnectionPattern";
            this.comboBoxConnectionPattern.Size = new System.Drawing.Size(121, 21);
            this.comboBoxConnectionPattern.TabIndex = 13;
            // 
            // tabPageTunneling
            // 
            this.tabPageTunneling.Controls.Add(this.groupBox3);
            this.tabPageTunneling.Controls.Add(this.groupBox2);
            this.tabPageTunneling.Location = new System.Drawing.Point(4, 22);
            this.tabPageTunneling.Name = "tabPageTunneling";
            this.tabPageTunneling.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageTunneling.Size = new System.Drawing.Size(583, 272);
            this.tabPageTunneling.TabIndex = 5;
            this.tabPageTunneling.Text = "Tunneling";
            this.tabPageTunneling.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label11);
            this.groupBox3.Controls.Add(this.checkBoxTunnelEndpointUseEncryption);
            this.groupBox3.Controls.Add(this.checkBoxEndpointIsTunnel);
            this.groupBox3.Controls.Add(this.textBoxTunnelEndpointPreSharedKey);
            this.groupBox3.Controls.Add(this.checkBoxTunnelEndpointUseCompression);
            this.groupBox3.Location = new System.Drawing.Point(317, 6);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(260, 167);
            this.groupBox3.TabIndex = 21;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Outgoing / Endpoints";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(3, 93);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(84, 13);
            this.label11.TabIndex = 23;
            this.label11.Text = "Pre-Shared Key:";
            // 
            // checkBoxTunnelEndpointUseEncryption
            // 
            this.checkBoxTunnelEndpointUseEncryption.AutoSize = true;
            this.checkBoxTunnelEndpointUseEncryption.Enabled = false;
            this.checkBoxTunnelEndpointUseEncryption.Location = new System.Drawing.Point(6, 65);
            this.checkBoxTunnelEndpointUseEncryption.Name = "checkBoxTunnelEndpointUseEncryption";
            this.checkBoxTunnelEndpointUseEncryption.Size = new System.Drawing.Size(104, 17);
            this.checkBoxTunnelEndpointUseEncryption.TabIndex = 22;
            this.checkBoxTunnelEndpointUseEncryption.Text = "Use Encryption?";
            this.checkBoxTunnelEndpointUseEncryption.UseVisualStyleBackColor = true;
            // 
            // checkBoxEndpointIsTunnel
            // 
            this.checkBoxEndpointIsTunnel.AutoSize = true;
            this.checkBoxEndpointIsTunnel.Location = new System.Drawing.Point(6, 19);
            this.checkBoxEndpointIsTunnel.Name = "checkBoxEndpointIsTunnel";
            this.checkBoxEndpointIsTunnel.Size = new System.Drawing.Size(197, 17);
            this.checkBoxEndpointIsTunnel.TabIndex = 20;
            this.checkBoxEndpointIsTunnel.Text = "Is Tunnel? (Connecting to NetProxy)";
            this.checkBoxEndpointIsTunnel.UseVisualStyleBackColor = true;
            this.checkBoxEndpointIsTunnel.CheckedChanged += new System.EventHandler(this.checkBoxEndpointIsTunnel_CheckedChanged);
            // 
            // textBoxTunnelEndpointPreSharedKey
            // 
            this.textBoxTunnelEndpointPreSharedKey.Enabled = false;
            this.textBoxTunnelEndpointPreSharedKey.Location = new System.Drawing.Point(6, 109);
            this.textBoxTunnelEndpointPreSharedKey.Multiline = true;
            this.textBoxTunnelEndpointPreSharedKey.Name = "textBoxTunnelEndpointPreSharedKey";
            this.textBoxTunnelEndpointPreSharedKey.PasswordChar = '*';
            this.textBoxTunnelEndpointPreSharedKey.Size = new System.Drawing.Size(248, 49);
            this.textBoxTunnelEndpointPreSharedKey.TabIndex = 23;
            // 
            // checkBoxTunnelEndpointUseCompression
            // 
            this.checkBoxTunnelEndpointUseCompression.AutoSize = true;
            this.checkBoxTunnelEndpointUseCompression.Enabled = false;
            this.checkBoxTunnelEndpointUseCompression.Location = new System.Drawing.Point(6, 42);
            this.checkBoxTunnelEndpointUseCompression.Name = "checkBoxTunnelEndpointUseCompression";
            this.checkBoxTunnelEndpointUseCompression.Size = new System.Drawing.Size(114, 17);
            this.checkBoxTunnelEndpointUseCompression.TabIndex = 21;
            this.checkBoxTunnelEndpointUseCompression.Text = "Use Compression?";
            this.checkBoxTunnelEndpointUseCompression.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label12);
            this.groupBox2.Controls.Add(this.checkBoxTunnelBindingUseEncryption);
            this.groupBox2.Controls.Add(this.textBoxTunnelBindingPreSharedKey);
            this.groupBox2.Controls.Add(this.checkBoxTunnelBindingUseCompression);
            this.groupBox2.Controls.Add(this.checkBoxBindingIsTunnel);
            this.groupBox2.Location = new System.Drawing.Point(6, 6);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(260, 167);
            this.groupBox2.TabIndex = 20;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Incoming / Binding";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(3, 93);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(84, 13);
            this.label12.TabIndex = 27;
            this.label12.Text = "Pre-Shared Key:";
            // 
            // checkBoxTunnelBindingUseEncryption
            // 
            this.checkBoxTunnelBindingUseEncryption.AutoSize = true;
            this.checkBoxTunnelBindingUseEncryption.Enabled = false;
            this.checkBoxTunnelBindingUseEncryption.Location = new System.Drawing.Point(6, 65);
            this.checkBoxTunnelBindingUseEncryption.Name = "checkBoxTunnelBindingUseEncryption";
            this.checkBoxTunnelBindingUseEncryption.Size = new System.Drawing.Size(104, 17);
            this.checkBoxTunnelBindingUseEncryption.TabIndex = 18;
            this.checkBoxTunnelBindingUseEncryption.Text = "Use Encryption?";
            this.checkBoxTunnelBindingUseEncryption.UseVisualStyleBackColor = true;
            // 
            // textBoxTunnelBindingPreSharedKey
            // 
            this.textBoxTunnelBindingPreSharedKey.Enabled = false;
            this.textBoxTunnelBindingPreSharedKey.Location = new System.Drawing.Point(6, 109);
            this.textBoxTunnelBindingPreSharedKey.Multiline = true;
            this.textBoxTunnelBindingPreSharedKey.Name = "textBoxTunnelBindingPreSharedKey";
            this.textBoxTunnelBindingPreSharedKey.PasswordChar = '*';
            this.textBoxTunnelBindingPreSharedKey.Size = new System.Drawing.Size(248, 49);
            this.textBoxTunnelBindingPreSharedKey.TabIndex = 19;
            // 
            // checkBoxTunnelBindingUseCompression
            // 
            this.checkBoxTunnelBindingUseCompression.AutoSize = true;
            this.checkBoxTunnelBindingUseCompression.Enabled = false;
            this.checkBoxTunnelBindingUseCompression.Location = new System.Drawing.Point(6, 42);
            this.checkBoxTunnelBindingUseCompression.Name = "checkBoxTunnelBindingUseCompression";
            this.checkBoxTunnelBindingUseCompression.Size = new System.Drawing.Size(114, 17);
            this.checkBoxTunnelBindingUseCompression.TabIndex = 17;
            this.checkBoxTunnelBindingUseCompression.Text = "Use Compression?";
            this.checkBoxTunnelBindingUseCompression.UseVisualStyleBackColor = true;
            // 
            // checkBoxBindingIsTunnel
            // 
            this.checkBoxBindingIsTunnel.AutoSize = true;
            this.checkBoxBindingIsTunnel.Location = new System.Drawing.Point(6, 19);
            this.checkBoxBindingIsTunnel.Name = "checkBoxBindingIsTunnel";
            this.checkBoxBindingIsTunnel.Size = new System.Drawing.Size(208, 17);
            this.checkBoxBindingIsTunnel.TabIndex = 16;
            this.checkBoxBindingIsTunnel.Text = "Is Tunnel? (Connection from NetProxy)";
            this.checkBoxBindingIsTunnel.UseVisualStyleBackColor = true;
            this.checkBoxBindingIsTunnel.CheckStateChanged += new System.EventHandler(this.checkBoxBindingIsTunnel_CheckStateChanged);
            // 
            // tabPageAdvanced
            // 
            this.tabPageAdvanced.Controls.Add(this.groupBox4);
            this.tabPageAdvanced.Controls.Add(this.groupBox1);
            this.tabPageAdvanced.Location = new System.Drawing.Point(4, 22);
            this.tabPageAdvanced.Name = "tabPageAdvanced";
            this.tabPageAdvanced.Size = new System.Drawing.Size(583, 272);
            this.tabPageAdvanced.TabIndex = 3;
            this.tabPageAdvanced.Text = "Advanced";
            this.tabPageAdvanced.UseVisualStyleBackColor = true;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.textBoxEncryptionInitTimeout);
            this.groupBox4.Controls.Add(this.textBoxStickySessionCacheExpiration);
            this.groupBox4.Controls.Add(this.label13);
            this.groupBox4.Controls.Add(this.label15);
            this.groupBox4.Controls.Add(this.label14);
            this.groupBox4.Controls.Add(this.textBoxSpinLockCount);
            this.groupBox4.Location = new System.Drawing.Point(254, 15);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(308, 119);
            this.groupBox4.TabIndex = 18;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Misc. (Super Advanced)";
            // 
            // textBoxEncryptionInitTimeout
            // 
            this.textBoxEncryptionInitTimeout.Location = new System.Drawing.Point(189, 19);
            this.textBoxEncryptionInitTimeout.Name = "textBoxEncryptionInitTimeout";
            this.textBoxEncryptionInitTimeout.Size = new System.Drawing.Size(100, 20);
            this.textBoxEncryptionInitTimeout.TabIndex = 13;
            // 
            // textBoxStickySessionCacheExpiration
            // 
            this.textBoxStickySessionCacheExpiration.Location = new System.Drawing.Point(189, 74);
            this.textBoxStickySessionCacheExpiration.Name = "textBoxStickySessionCacheExpiration";
            this.textBoxStickySessionCacheExpiration.Size = new System.Drawing.Size(100, 20);
            this.textBoxStickySessionCacheExpiration.TabIndex = 17;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(12, 22);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(171, 13);
            this.label13.TabIndex = 12;
            this.label13.Text = "Encryption Initilization Timeout (ms)";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(10, 77);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(173, 13);
            this.label15.TabIndex = 16;
            this.label15.Text = "Sticky Session Cache Expiration (s)";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(97, 51);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(86, 13);
            this.label14.TabIndex = 14;
            this.label14.Text = "Spin-Lock Count";
            // 
            // textBoxSpinLockCount
            // 
            this.textBoxSpinLockCount.Location = new System.Drawing.Point(189, 48);
            this.textBoxSpinLockCount.Name = "textBoxSpinLockCount";
            this.textBoxSpinLockCount.Size = new System.Drawing.Size(100, 20);
            this.textBoxSpinLockCount.TabIndex = 15;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textBoxMaxBufferSize);
            this.groupBox1.Controls.Add(this.textBoxAcceptBacklogSize);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.labelAcceptBacklogSize);
            this.groupBox1.Controls.Add(this.textBoxInitialBufferSize);
            this.groupBox1.Location = new System.Drawing.Point(12, 15);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(236, 119);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Buffers";
            // 
            // textBoxMaxBufferSize
            // 
            this.textBoxMaxBufferSize.Location = new System.Drawing.Point(115, 48);
            this.textBoxMaxBufferSize.Name = "textBoxMaxBufferSize";
            this.textBoxMaxBufferSize.Size = new System.Drawing.Size(100, 20);
            this.textBoxMaxBufferSize.TabIndex = 11;
            // 
            // textBoxAcceptBacklogSize
            // 
            this.textBoxAcceptBacklogSize.Location = new System.Drawing.Point(115, 74);
            this.textBoxAcceptBacklogSize.Name = "textBoxAcceptBacklogSize";
            this.textBoxAcceptBacklogSize.Size = new System.Drawing.Size(100, 20);
            this.textBoxAcceptBacklogSize.TabIndex = 12;
            // 
            // textBoxInitialBufferSize
            // 
            this.textBoxInitialBufferSize.Location = new System.Drawing.Point(115, 22);
            this.textBoxInitialBufferSize.Name = "textBoxInitialBufferSize";
            this.textBoxInitialBufferSize.Size = new System.Drawing.Size(100, 20);
            this.textBoxInitialBufferSize.TabIndex = 10;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Location = new System.Drawing.Point(528, 319);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 25;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(447, 319);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(75, 23);
            this.buttonSave.TabIndex = 24;
            this.buttonSave.Text = "Save";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // FormRoute
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(618, 353);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.tabControl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormRoute";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Route";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormRoute_FormClosed);
            this.Shown += new System.EventHandler(this.FormRoute_Shown);
            this.tabControl.ResumeLayout(false);
            this.tabPageGeneral.ResumeLayout(false);
            this.tabPageGeneral.PerformLayout();
            this.tabPageBindings.ResumeLayout(false);
            this.tabPageBindings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewBindings)).EndInit();
            this.tabPageHTTPHeaders.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewHTTPHeaders)).EndInit();
            this.tabPageEndpoints.ResumeLayout(false);
            this.tabPageEndpoints.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEndpoints)).EndInit();
            this.tabPageTunneling.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.tabPageAdvanced.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelAcceptBacklogSize;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox checkBoxListenOnAllAddresses;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPageGeneral;
        private System.Windows.Forms.ComboBox comboBoxTrafficType;
        private System.Windows.Forms.TextBox textBoxRouteName;
        private System.Windows.Forms.TabPage tabPageBindings;
        private System.Windows.Forms.TextBox textBoxListenPort;
        private System.Windows.Forms.TabPage tabPageHTTPHeaders;
        private System.Windows.Forms.TabPage tabPageAdvanced;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textBoxMaxBufferSize;
        private System.Windows.Forms.TextBox textBoxInitialBufferSize;
        private System.Windows.Forms.TabPage tabPageEndpoints;
        private System.Windows.Forms.TextBox textBoxAcceptBacklogSize;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox comboBoxConnectionPattern;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.DataGridView dataGridViewEndpoints;
        private System.Windows.Forms.DataGridView dataGridViewBindings;
        private System.Windows.Forms.DataGridView dataGridViewHTTPHeaders;
        private System.Windows.Forms.CheckBox checkBoxListenAutoStart;
        private System.Windows.Forms.TextBox textBoxDescription;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ColumnHTTPHeadersEnabled;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnHTTPHeadersType;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnHTTPHeadersVerb;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnHTTPHeadersHeader;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnHTTPHeadersAction;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnHTTPHeadersValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnHTTPHeadersDescription;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ColumnBindingsEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnBindingsIPAddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnBindingsDescription;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ColumnEndpointsEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnEndpointsAddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnEndpointsPort;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnEndpointsDescription;
        private System.Windows.Forms.ComboBox comboBoxBindingProtocol;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TabPage tabPageTunneling;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.CheckBox checkBoxTunnelEndpointUseEncryption;
        private System.Windows.Forms.CheckBox checkBoxEndpointIsTunnel;
        private System.Windows.Forms.TextBox textBoxTunnelEndpointPreSharedKey;
        private System.Windows.Forms.CheckBox checkBoxTunnelEndpointUseCompression;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.CheckBox checkBoxTunnelBindingUseEncryption;
        private System.Windows.Forms.TextBox textBoxTunnelBindingPreSharedKey;
        private System.Windows.Forms.CheckBox checkBoxTunnelBindingUseCompression;
        private System.Windows.Forms.CheckBox checkBoxBindingIsTunnel;
        private System.Windows.Forms.CheckBox checkBoxUseStickySessions;
        private System.Windows.Forms.TextBox textBoxSpinLockCount;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox textBoxEncryptionInitTimeout;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox textBoxStickySessionCacheExpiration;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.GroupBox groupBox4;
    }
}