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
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormProxy));
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
            tabPageHTTPHeaders = new TabPage();
            dataGridViewHTTPHeaders = new DataGridView();
            ColumnHTTPHeadersEnabled = new DataGridViewCheckBoxColumn();
            ColumnHTTPHeadersType = new DataGridViewComboBoxColumn();
            ColumnHTTPHeadersVerb = new DataGridViewComboBoxColumn();
            ColumnHTTPHeadersHeader = new DataGridViewTextBoxColumn();
            ColumnHTTPHeadersAction = new DataGridViewComboBoxColumn();
            ColumnHTTPHeadersValue = new DataGridViewTextBoxColumn();
            ColumnHTTPHeadersDescription = new DataGridViewTextBoxColumn();
            tabPageEndpoints = new TabPage();
            checkBoxUseStickySessions = new CheckBox();
            dataGridViewEndpoints = new DataGridView();
            ColumnEndpointsEnabled = new DataGridViewCheckBoxColumn();
            ColumnEndpointsAddress = new DataGridViewTextBoxColumn();
            ColumnEndpointsPort = new DataGridViewTextBoxColumn();
            ColumnEndpointsDescription = new DataGridViewTextBoxColumn();
            label8 = new Label();
            label9 = new Label();
            comboBoxConnectionPattern = new ComboBox();
            tabPageAdvanced = new TabPage();
            groupBox4 = new GroupBox();
            textBoxStickySessionCacheExpiration = new TextBox();
            label15 = new Label();
            groupBox1 = new GroupBox();
            textBoxMaxBufferSize = new TextBox();
            textBoxAcceptBacklogSize = new TextBox();
            textBoxInitialBufferSize = new TextBox();
            buttonCancel = new Button();
            buttonSave = new Button();
            tabControl.SuspendLayout();
            tabPageGeneral.SuspendLayout();
            tabPageBindings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewBindings).BeginInit();
            tabPageHTTPHeaders.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewHTTPHeaders).BeginInit();
            tabPageEndpoints.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewEndpoints).BeginInit();
            tabPageAdvanced.SuspendLayout();
            groupBox4.SuspendLayout();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(34, 16);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(39, 15);
            label1.TabIndex = 0;
            label1.Text = "Name";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(7, 48);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(66, 15);
            label2.TabIndex = 1;
            label2.Text = "Traffic Type";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(102, 28);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(59, 15);
            label3.TabIndex = 2;
            label3.Text = "Initial Size";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(42, 18);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(63, 15);
            label4.TabIndex = 3;
            label4.Text = "Listen Port";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(108, 57);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(53, 15);
            label5.TabIndex = 4;
            label5.Text = "Max Size";
            // 
            // labelAcceptBacklogSize
            // 
            labelAcceptBacklogSize.AutoSize = true;
            labelAcceptBacklogSize.Location = new Point(49, 30);
            labelAcceptBacklogSize.Margin = new Padding(4, 0, 4, 0);
            labelAcceptBacklogSize.Name = "labelAcceptBacklogSize";
            labelAcceptBacklogSize.Size = new Size(112, 15);
            labelAcceptBacklogSize.TabIndex = 5;
            labelAcceptBacklogSize.Text = "Accept Backlog Size";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(10, 87);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(94, 15);
            label7.TabIndex = 6;
            label7.Text = "Listen Addresses";
            // 
            // checkBoxListenOnAllAddresses
            // 
            checkBoxListenOnAllAddresses.AutoSize = true;
            checkBoxListenOnAllAddresses.Location = new Point(14, 285);
            checkBoxListenOnAllAddresses.Margin = new Padding(4, 3, 4, 3);
            checkBoxListenOnAllAddresses.Name = "checkBoxListenOnAllAddresses";
            checkBoxListenOnAllAddresses.Size = new Size(152, 19);
            checkBoxListenOnAllAddresses.TabIndex = 8;
            checkBoxListenOnAllAddresses.Text = "Listen on All Addresses?";
            checkBoxListenOnAllAddresses.UseVisualStyleBackColor = true;
            checkBoxListenOnAllAddresses.CheckedChanged += checkBoxListenOnAllAddresses_CheckedChanged;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabPageGeneral);
            tabControl.Controls.Add(tabPageBindings);
            tabControl.Controls.Add(tabPageHTTPHeaders);
            tabControl.Controls.Add(tabPageEndpoints);
            tabControl.Controls.Add(tabPageAdvanced);
            tabControl.Location = new Point(14, 14);
            tabControl.Margin = new Padding(0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(596, 344);
            tabControl.TabIndex = 10;
            // 
            // tabPageGeneral
            // 
            tabPageGeneral.BackColor = SystemColors.Control;
            tabPageGeneral.Controls.Add(textBoxDescription);
            tabPageGeneral.Controls.Add(label6);
            tabPageGeneral.Controls.Add(checkBoxListenAutoStart);
            tabPageGeneral.Controls.Add(comboBoxTrafficType);
            tabPageGeneral.Controls.Add(textBoxProxyName);
            tabPageGeneral.Controls.Add(label1);
            tabPageGeneral.Controls.Add(label2);
            tabPageGeneral.Location = new Point(4, 24);
            tabPageGeneral.Margin = new Padding(0);
            tabPageGeneral.Name = "tabPageGeneral";
            tabPageGeneral.Size = new Size(588, 316);
            tabPageGeneral.TabIndex = 0;
            tabPageGeneral.Text = "General";
            // 
            // textBoxDescription
            // 
            textBoxDescription.AcceptsReturn = true;
            textBoxDescription.Location = new Point(85, 114);
            textBoxDescription.Margin = new Padding(4, 3, 4, 3);
            textBoxDescription.Multiline = true;
            textBoxDescription.Name = "textBoxDescription";
            textBoxDescription.ScrollBars = ScrollBars.Both;
            textBoxDescription.Size = new Size(484, 175);
            textBoxDescription.TabIndex = 4;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(6, 114);
            label6.Margin = new Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(67, 15);
            label6.TabIndex = 13;
            label6.Text = "Description";
            // 
            // checkBoxListenAutoStart
            // 
            checkBoxListenAutoStart.AutoSize = true;
            checkBoxListenAutoStart.Location = new Point(85, 76);
            checkBoxListenAutoStart.Margin = new Padding(4, 3, 4, 3);
            checkBoxListenAutoStart.Name = "checkBoxListenAutoStart";
            checkBoxListenAutoStart.Size = new Size(84, 19);
            checkBoxListenAutoStart.TabIndex = 3;
            checkBoxListenAutoStart.Text = "Auto Start?";
            checkBoxListenAutoStart.UseVisualStyleBackColor = true;
            // 
            // comboBoxTrafficType
            // 
            comboBoxTrafficType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxTrafficType.FormattingEnabled = true;
            comboBoxTrafficType.Location = new Point(85, 45);
            comboBoxTrafficType.Margin = new Padding(4, 3, 4, 3);
            comboBoxTrafficType.Name = "comboBoxTrafficType";
            comboBoxTrafficType.Size = new Size(140, 23);
            comboBoxTrafficType.TabIndex = 2;
            comboBoxTrafficType.SelectedIndexChanged += comboBoxTrafficType_SelectedIndexChanged;
            // 
            // textBoxProxyName
            // 
            textBoxProxyName.Location = new Point(85, 13);
            textBoxProxyName.Margin = new Padding(4, 3, 4, 3);
            textBoxProxyName.Name = "textBoxProxyName";
            textBoxProxyName.Size = new Size(484, 23);
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
            tabPageBindings.Margin = new Padding(0);
            tabPageBindings.Name = "tabPageBindings";
            tabPageBindings.Size = new Size(588, 316);
            tabPageBindings.TabIndex = 1;
            tabPageBindings.Text = "Bindings";
            // 
            // comboBoxBindingProtocol
            // 
            comboBoxBindingProtocol.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxBindingProtocol.FormattingEnabled = true;
            comboBoxBindingProtocol.Location = new Point(115, 45);
            comboBoxBindingProtocol.Margin = new Padding(4, 3, 4, 3);
            comboBoxBindingProtocol.Name = "comboBoxBindingProtocol";
            comboBoxBindingProtocol.Size = new Size(140, 23);
            comboBoxBindingProtocol.TabIndex = 6;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(10, 50);
            label10.Margin = new Padding(4, 0, 4, 0);
            label10.Name = "label10";
            label10.Size = new Size(96, 15);
            label10.TabIndex = 11;
            label10.Text = "Binding Protocol";
            // 
            // dataGridViewBindings
            // 
            dataGridViewBindings.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewBindings.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewBindings.Columns.AddRange(new DataGridViewColumn[] { ColumnBindingsEnabled, ColumnBindingsIPAddress, ColumnBindingsDescription });
            dataGridViewBindings.Location = new Point(10, 105);
            dataGridViewBindings.Margin = new Padding(4, 3, 4, 3);
            dataGridViewBindings.Name = "dataGridViewBindings";
            dataGridViewBindings.Size = new Size(565, 173);
            dataGridViewBindings.TabIndex = 7;
            dataGridViewBindings.Click += dataGridViewBindings_Click;
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
            textBoxListenPort.Location = new Point(115, 15);
            textBoxListenPort.Margin = new Padding(4, 3, 4, 3);
            textBoxListenPort.Name = "textBoxListenPort";
            textBoxListenPort.Size = new Size(140, 23);
            textBoxListenPort.TabIndex = 5;
            // 
            // tabPageHTTPHeaders
            // 
            tabPageHTTPHeaders.BackColor = SystemColors.Control;
            tabPageHTTPHeaders.Controls.Add(dataGridViewHTTPHeaders);
            tabPageHTTPHeaders.Location = new Point(4, 24);
            tabPageHTTPHeaders.Margin = new Padding(0);
            tabPageHTTPHeaders.Name = "tabPageHTTPHeaders";
            tabPageHTTPHeaders.Size = new Size(588, 316);
            tabPageHTTPHeaders.TabIndex = 2;
            tabPageHTTPHeaders.Text = "HTTPHeaders";
            // 
            // dataGridViewHTTPHeaders
            // 
            dataGridViewHTTPHeaders.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewHTTPHeaders.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewHTTPHeaders.Columns.AddRange(new DataGridViewColumn[] { ColumnHTTPHeadersEnabled, ColumnHTTPHeadersType, ColumnHTTPHeadersVerb, ColumnHTTPHeadersHeader, ColumnHTTPHeadersAction, ColumnHTTPHeadersValue, ColumnHTTPHeadersDescription });
            dataGridViewHTTPHeaders.Dock = DockStyle.Fill;
            dataGridViewHTTPHeaders.Location = new Point(0, 0);
            dataGridViewHTTPHeaders.Margin = new Padding(4, 3, 4, 3);
            dataGridViewHTTPHeaders.Name = "dataGridViewHTTPHeaders";
            dataGridViewHTTPHeaders.Size = new Size(588, 316);
            dataGridViewHTTPHeaders.TabIndex = 9;
            dataGridViewHTTPHeaders.Click += dataGridViewHTTPHeaders_Click;
            // 
            // ColumnHTTPHeadersEnabled
            // 
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ColumnHTTPHeadersEnabled.DefaultCellStyle = dataGridViewCellStyle2;
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
            // tabPageEndpoints
            // 
            tabPageEndpoints.BackColor = SystemColors.Control;
            tabPageEndpoints.Controls.Add(checkBoxUseStickySessions);
            tabPageEndpoints.Controls.Add(dataGridViewEndpoints);
            tabPageEndpoints.Controls.Add(label8);
            tabPageEndpoints.Controls.Add(label9);
            tabPageEndpoints.Controls.Add(comboBoxConnectionPattern);
            tabPageEndpoints.Location = new Point(4, 24);
            tabPageEndpoints.Margin = new Padding(0);
            tabPageEndpoints.Name = "tabPageEndpoints";
            tabPageEndpoints.Size = new Size(588, 316);
            tabPageEndpoints.TabIndex = 4;
            tabPageEndpoints.Text = "Endpoints";
            // 
            // checkBoxUseStickySessions
            // 
            checkBoxUseStickySessions.AutoSize = true;
            checkBoxUseStickySessions.Location = new Point(282, 13);
            checkBoxUseStickySessions.Margin = new Padding(4, 3, 4, 3);
            checkBoxUseStickySessions.Name = "checkBoxUseStickySessions";
            checkBoxUseStickySessions.Size = new Size(131, 19);
            checkBoxUseStickySessions.TabIndex = 19;
            checkBoxUseStickySessions.Text = "Use Sticky Sessions?";
            checkBoxUseStickySessions.UseVisualStyleBackColor = true;
            // 
            // dataGridViewEndpoints
            // 
            dataGridViewEndpoints.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewEndpoints.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewEndpoints.Columns.AddRange(new DataGridViewColumn[] { ColumnEndpointsEnabled, ColumnEndpointsAddress, ColumnEndpointsPort, ColumnEndpointsDescription });
            dataGridViewEndpoints.Location = new Point(16, 57);
            dataGridViewEndpoints.Margin = new Padding(4, 3, 4, 3);
            dataGridViewEndpoints.Name = "dataGridViewEndpoints";
            dataGridViewEndpoints.Size = new Size(559, 243);
            dataGridViewEndpoints.TabIndex = 14;
            // 
            // ColumnEndpointsEnabled
            // 
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ColumnEndpointsEnabled.DefaultCellStyle = dataGridViewCellStyle3;
            ColumnEndpointsEnabled.HeaderText = "Enabled";
            ColumnEndpointsEnabled.Name = "ColumnEndpointsEnabled";
            ColumnEndpointsEnabled.Width = 55;
            // 
            // ColumnEndpointsAddress
            // 
            ColumnEndpointsAddress.HeaderText = "Address";
            ColumnEndpointsAddress.Name = "ColumnEndpointsAddress";
            ColumnEndpointsAddress.Width = 150;
            // 
            // ColumnEndpointsPort
            // 
            ColumnEndpointsPort.HeaderText = "Port";
            ColumnEndpointsPort.Name = "ColumnEndpointsPort";
            ColumnEndpointsPort.Width = 50;
            // 
            // ColumnEndpointsDescription
            // 
            ColumnEndpointsDescription.HeaderText = "Description";
            ColumnEndpointsDescription.Name = "ColumnEndpointsDescription";
            ColumnEndpointsDescription.Width = 250;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(13, 38);
            label8.Margin = new Padding(4, 0, 4, 0);
            label8.Name = "label8";
            label8.Size = new Size(104, 15);
            label8.TabIndex = 13;
            label8.Text = "Remote Endpoints";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(13, 14);
            label9.Margin = new Padding(4, 0, 4, 0);
            label9.Name = "label9";
            label9.Size = new Size(110, 15);
            label9.TabIndex = 12;
            label9.Text = "Connection Pattern";
            // 
            // comboBoxConnectionPattern
            // 
            comboBoxConnectionPattern.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxConnectionPattern.FormattingEnabled = true;
            comboBoxConnectionPattern.Location = new Point(134, 10);
            comboBoxConnectionPattern.Margin = new Padding(4, 3, 4, 3);
            comboBoxConnectionPattern.Name = "comboBoxConnectionPattern";
            comboBoxConnectionPattern.Size = new Size(140, 23);
            comboBoxConnectionPattern.TabIndex = 13;
            // 
            // tabPageAdvanced
            // 
            tabPageAdvanced.BackColor = SystemColors.Control;
            tabPageAdvanced.Controls.Add(groupBox4);
            tabPageAdvanced.Controls.Add(groupBox1);
            tabPageAdvanced.Location = new Point(4, 24);
            tabPageAdvanced.Margin = new Padding(0);
            tabPageAdvanced.Name = "tabPageAdvanced";
            tabPageAdvanced.Size = new Size(588, 316);
            tabPageAdvanced.TabIndex = 3;
            tabPageAdvanced.Text = "Advanced";
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(textBoxStickySessionCacheExpiration);
            groupBox4.Controls.Add(textBoxAcceptBacklogSize);
            groupBox4.Controls.Add(label15);
            groupBox4.Controls.Add(labelAcceptBacklogSize);
            groupBox4.Location = new Point(14, 131);
            groupBox4.Margin = new Padding(4, 3, 4, 3);
            groupBox4.Name = "groupBox4";
            groupBox4.Padding = new Padding(4, 3, 4, 3);
            groupBox4.Size = new Size(309, 113);
            groupBox4.TabIndex = 18;
            groupBox4.TabStop = false;
            groupBox4.Text = "Misc. (Super Advanced)";
            // 
            // textBoxStickySessionCacheExpiration
            // 
            textBoxStickySessionCacheExpiration.Location = new Point(169, 56);
            textBoxStickySessionCacheExpiration.Margin = new Padding(4, 3, 4, 3);
            textBoxStickySessionCacheExpiration.Name = "textBoxStickySessionCacheExpiration";
            textBoxStickySessionCacheExpiration.Size = new Size(116, 23);
            textBoxStickySessionCacheExpiration.TabIndex = 17;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new Point(9, 59);
            label15.Margin = new Padding(4, 0, 4, 0);
            label15.Name = "label15";
            label15.Size = new Size(152, 15);
            label15.TabIndex = 16;
            label15.Text = "Sticky Session Expiration (s)";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(textBoxMaxBufferSize);
            groupBox1.Controls.Add(label5);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(textBoxInitialBufferSize);
            groupBox1.Location = new Point(14, 17);
            groupBox1.Margin = new Padding(4, 3, 4, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4, 3, 4, 3);
            groupBox1.Size = new Size(309, 99);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Buffers";
            // 
            // textBoxMaxBufferSize
            // 
            textBoxMaxBufferSize.Location = new Point(169, 54);
            textBoxMaxBufferSize.Margin = new Padding(4, 3, 4, 3);
            textBoxMaxBufferSize.Name = "textBoxMaxBufferSize";
            textBoxMaxBufferSize.Size = new Size(116, 23);
            textBoxMaxBufferSize.TabIndex = 11;
            // 
            // textBoxAcceptBacklogSize
            // 
            textBoxAcceptBacklogSize.Location = new Point(169, 27);
            textBoxAcceptBacklogSize.Margin = new Padding(4, 3, 4, 3);
            textBoxAcceptBacklogSize.Name = "textBoxAcceptBacklogSize";
            textBoxAcceptBacklogSize.Size = new Size(116, 23);
            textBoxAcceptBacklogSize.TabIndex = 12;
            // 
            // textBoxInitialBufferSize
            // 
            textBoxInitialBufferSize.Location = new Point(169, 25);
            textBoxInitialBufferSize.Margin = new Padding(4, 3, 4, 3);
            textBoxInitialBufferSize.Name = "textBoxInitialBufferSize";
            textBoxInitialBufferSize.Size = new Size(116, 23);
            textBoxInitialBufferSize.TabIndex = 10;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(522, 368);
            buttonCancel.Margin = new Padding(4, 3, 4, 3);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(88, 27);
            buttonCancel.TabIndex = 25;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // buttonSave
            // 
            buttonSave.Location = new Point(426, 368);
            buttonSave.Margin = new Padding(4, 3, 4, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(88, 27);
            buttonSave.TabIndex = 24;
            buttonSave.Text = "Save";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += buttonSave_Click;
            // 
            // FormProxy
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(622, 407);
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
            tabPageHTTPHeaders.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewHTTPHeaders).EndInit();
            tabPageEndpoints.ResumeLayout(false);
            tabPageEndpoints.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewEndpoints).EndInit();
            tabPageAdvanced.ResumeLayout(false);
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
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
        private DataGridViewCheckBoxColumn ColumnHTTPHeadersEnabled;
        private DataGridViewComboBoxColumn ColumnHTTPHeadersType;
        private DataGridViewComboBoxColumn ColumnHTTPHeadersVerb;
        private DataGridViewTextBoxColumn ColumnHTTPHeadersHeader;
        private DataGridViewComboBoxColumn ColumnHTTPHeadersAction;
        private DataGridViewTextBoxColumn ColumnHTTPHeadersValue;
        private DataGridViewTextBoxColumn ColumnHTTPHeadersDescription;
        private DataGridViewCheckBoxColumn ColumnEndpointsEnabled;
        private DataGridViewTextBoxColumn ColumnEndpointsAddress;
        private DataGridViewTextBoxColumn ColumnEndpointsPort;
        private DataGridViewTextBoxColumn ColumnEndpointsDescription;
        private DataGridViewCheckBoxColumn ColumnBindingsEnabled;
        private DataGridViewTextBoxColumn ColumnBindingsIPAddress;
        private DataGridViewTextBoxColumn ColumnBindingsDescription;
    }
}