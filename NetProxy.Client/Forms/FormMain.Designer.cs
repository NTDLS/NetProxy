namespace NetProxy.Client.Forms
{
    partial class FormMain
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            dataGridViewProxys = new DataGridView();
            ColumnStatus = new DataGridViewImageColumn();
            ColumnId = new DataGridViewTextBoxColumn();
            ColumnName = new DataGridViewTextBoxColumn();
            ColumnProxyType = new DataGridViewTextBoxColumn();
            ColummListenPort = new DataGridViewTextBoxColumn();
            ColumnBytesTransferred = new DataGridViewTextBoxColumn();
            ColumnTotalConnections = new DataGridViewTextBoxColumn();
            ColumnCurrentConnections = new DataGridViewTextBoxColumn();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            changeConnectionToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            configurationToolStripMenuItem = new ToolStripMenuItem();
            pluginsToolStripMenuItem = new ToolStripMenuItem();
            certificatesToolStripMenuItem = new ToolStripMenuItem();
            languageToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            proxyToolStripMenuItem = new ToolStripMenuItem();
            addToolStripMenuItem = new ToolStripMenuItem();
            refreshToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            startToolStripMenuItem = new ToolStripMenuItem();
            stopToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            deleteToolStripMenuItem = new ToolStripMenuItem();
            statusStripMain = new StatusStrip();
            chartPerformance = new System.Windows.Forms.DataVisualization.Charting.Chart();
            splitContainerOuter = new SplitContainer();
            splitContainerInner = new SplitContainer();
            dataGridViewConnections = new DataGridView();
            ColumnConnSourceAddress = new DataGridViewTextBoxColumn();
            ColumnConnSourceHost = new DataGridViewTextBoxColumn();
            ColumnConnSourcePort = new DataGridViewTextBoxColumn();
            ColumnConnDestinationAddress = new DataGridViewTextBoxColumn();
            ColumnConnDestinationHost = new DataGridViewTextBoxColumn();
            ColumnConnDestinationPort = new DataGridViewTextBoxColumn();
            ColumnConnProtocol = new DataGridViewTextBoxColumn();
            ColumnConnVersion = new DataGridViewTextBoxColumn();
            ColumnConnEncryption = new DataGridViewTextBoxColumn();
            ColumnConnBytesIn = new DataGridViewTextBoxColumn();
            ColumnConnBytesOut = new DataGridViewTextBoxColumn();
            ColumnConnInPerSecond = new DataGridViewTextBoxColumn();
            ColumnConnOutPerSecond = new DataGridViewTextBoxColumn();
            ColumnConnStarted = new DataGridViewTextBoxColumn();
            ColumnConnDuration = new DataGridViewTextBoxColumn();
            ColumnConnIdle = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dataGridViewProxys).BeginInit();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)chartPerformance).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainerOuter).BeginInit();
            splitContainerOuter.Panel1.SuspendLayout();
            splitContainerOuter.Panel2.SuspendLayout();
            splitContainerOuter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerInner).BeginInit();
            splitContainerInner.Panel1.SuspendLayout();
            splitContainerInner.Panel2.SuspendLayout();
            splitContainerInner.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewConnections).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewProxys
            // 
            dataGridViewProxys.AllowUserToAddRows = false;
            dataGridViewProxys.AllowUserToDeleteRows = false;
            dataGridViewProxys.AllowUserToResizeRows = false;
            dataGridViewProxys.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewProxys.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewProxys.Columns.AddRange(new DataGridViewColumn[] { ColumnStatus, ColumnId, ColumnName, ColumnProxyType, ColummListenPort, ColumnBytesTransferred, ColumnTotalConnections, ColumnCurrentConnections });
            dataGridViewProxys.Dock = DockStyle.Fill;
            dataGridViewProxys.EditMode = DataGridViewEditMode.EditProgrammatically;
            dataGridViewProxys.Location = new Point(0, 0);
            dataGridViewProxys.Margin = new Padding(4, 3, 4, 3);
            dataGridViewProxys.MultiSelect = false;
            dataGridViewProxys.Name = "dataGridViewProxys";
            dataGridViewProxys.ReadOnly = true;
            dataGridViewProxys.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewProxys.Size = new Size(784, 193);
            dataGridViewProxys.TabIndex = 0;
            dataGridViewProxys.CellDoubleClick += DataGridViewProxys_CellDoubleClick;
            dataGridViewProxys.ColumnHeaderMouseClick += DataGridViewProxys_ColumnHeaderMouseClick;
            dataGridViewProxys.MouseDown += DataGridViewProxys_MouseDown;
            // 
            // ColumnStatus
            // 
            ColumnStatus.Frozen = true;
            ColumnStatus.HeaderText = "Status";
            ColumnStatus.Name = "ColumnStatus";
            ColumnStatus.ReadOnly = true;
            ColumnStatus.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnStatus.Width = 45;
            // 
            // ColumnId
            // 
            ColumnId.DataPropertyName = "Id";
            ColumnId.HeaderText = "Id";
            ColumnId.Name = "ColumnId";
            ColumnId.ReadOnly = true;
            ColumnId.Visible = false;
            // 
            // ColumnName
            // 
            ColumnName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ColumnName.DataPropertyName = "Name";
            ColumnName.HeaderText = "Name";
            ColumnName.MinimumWidth = 150;
            ColumnName.Name = "ColumnName";
            ColumnName.ReadOnly = true;
            ColumnName.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // ColumnProxyType
            // 
            ColumnProxyType.DataPropertyName = "ProxyType";
            ColumnProxyType.HeaderText = "Type";
            ColumnProxyType.Name = "ColumnProxyType";
            ColumnProxyType.ReadOnly = true;
            ColumnProxyType.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnProxyType.Width = 115;
            // 
            // ColummListenPort
            // 
            ColummListenPort.DataPropertyName = "ListenPort";
            ColummListenPort.HeaderText = "Port";
            ColummListenPort.Name = "ColummListenPort";
            ColummListenPort.ReadOnly = true;
            ColummListenPort.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColummListenPort.Width = 50;
            // 
            // ColumnBytesTransferred
            // 
            ColumnBytesTransferred.HeaderText = "Bytes Transferred";
            ColumnBytesTransferred.Name = "ColumnBytesTransferred";
            ColumnBytesTransferred.ReadOnly = true;
            ColumnBytesTransferred.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnBytesTransferred.Width = 125;
            // 
            // ColumnTotalConnections
            // 
            ColumnTotalConnections.HeaderText = "Total Conn.";
            ColumnTotalConnections.Name = "ColumnTotalConnections";
            ColumnTotalConnections.ReadOnly = true;
            ColumnTotalConnections.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnTotalConnections.Width = 95;
            // 
            // ColumnCurrentConnections
            // 
            ColumnCurrentConnections.HeaderText = "Curr. Conn.";
            ColumnCurrentConnections.Name = "ColumnCurrentConnections";
            ColumnCurrentConnections.ReadOnly = true;
            ColumnCurrentConnections.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnCurrentConnections.Width = 95;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, settingsToolStripMenuItem, proxyToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(7, 2, 0, 2);
            menuStrip1.Size = new Size(784, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { changeConnectionToolStripMenuItem, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // changeConnectionToolStripMenuItem
            // 
            changeConnectionToolStripMenuItem.Name = "changeConnectionToolStripMenuItem";
            changeConnectionToolStripMenuItem.Size = new Size(180, 22);
            changeConnectionToolStripMenuItem.Text = "Change Connection";
            changeConnectionToolStripMenuItem.Click += ChangeConnectionToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(180, 22);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += ExitToolStripMenuItem_Click;
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { configurationToolStripMenuItem, certificatesToolStripMenuItem, pluginsToolStripMenuItem, languageToolStripMenuItem });
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(61, 20);
            settingsToolStripMenuItem.Text = "Settings";
            // 
            // configurationToolStripMenuItem
            // 
            configurationToolStripMenuItem.Name = "configurationToolStripMenuItem";
            configurationToolStripMenuItem.Size = new Size(148, 22);
            configurationToolStripMenuItem.Text = "Configuration";
            configurationToolStripMenuItem.Click += ConfigurationToolStripMenuItem_Click;
            // 
            // certificatesToolStripMenuItem
            // 
            certificatesToolStripMenuItem.Name = "certificatesToolStripMenuItem";
            certificatesToolStripMenuItem.Size = new Size(148, 22);
            certificatesToolStripMenuItem.Text = "Certificates...";
            certificatesToolStripMenuItem.Click += CertificatesToolStripMenuItem_Click;
            // 
            // pluginsToolStripMenuItem
            // 
            pluginsToolStripMenuItem.Name = "pluginsToolStripMenuItem";
            pluginsToolStripMenuItem.Size = new Size(148, 22);
            pluginsToolStripMenuItem.Text = "Plugins...";
            pluginsToolStripMenuItem.Click += PluginsToolStripMenuItem_Click;
            // 
            // languageToolStripMenuItem
            // 
            languageToolStripMenuItem.Name = "languageToolStripMenuItem";
            languageToolStripMenuItem.Size = new Size(148, 22);
            languageToolStripMenuItem.Text = "Language...";
            languageToolStripMenuItem.Click += LanguageToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(107, 22);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += AboutToolStripMenuItem_Click;
            // 
            // proxyToolStripMenuItem
            // 
            proxyToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { addToolStripMenuItem, refreshToolStripMenuItem, toolStripSeparator1, startToolStripMenuItem, stopToolStripMenuItem, editToolStripMenuItem, toolStripSeparator2, deleteToolStripMenuItem });
            proxyToolStripMenuItem.Name = "proxyToolStripMenuItem";
            proxyToolStripMenuItem.Size = new Size(48, 20);
            proxyToolStripMenuItem.Text = "Proxy";
            proxyToolStripMenuItem.DropDownOpening += ProxyToolStripMenuItem_DropDownOpening;
            // 
            // addToolStripMenuItem
            // 
            addToolStripMenuItem.Name = "addToolStripMenuItem";
            addToolStripMenuItem.Size = new Size(180, 22);
            addToolStripMenuItem.Text = "Add";
            addToolStripMenuItem.Click += AddToolStripMenuItem_Click;
            // 
            // refreshToolStripMenuItem
            // 
            refreshToolStripMenuItem.Name = "refreshToolStripMenuItem";
            refreshToolStripMenuItem.Size = new Size(180, 22);
            refreshToolStripMenuItem.Text = "Refresh";
            refreshToolStripMenuItem.Click += RefreshToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(177, 6);
            // 
            // startToolStripMenuItem
            // 
            startToolStripMenuItem.Name = "startToolStripMenuItem";
            startToolStripMenuItem.Size = new Size(180, 22);
            startToolStripMenuItem.Text = "Start";
            startToolStripMenuItem.Click += StartToolStripMenuItem_Click;
            // 
            // stopToolStripMenuItem
            // 
            stopToolStripMenuItem.Name = "stopToolStripMenuItem";
            stopToolStripMenuItem.Size = new Size(180, 22);
            stopToolStripMenuItem.Text = "Stop";
            stopToolStripMenuItem.Click += StopToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(180, 22);
            editToolStripMenuItem.Text = "Edit";
            editToolStripMenuItem.Click += EditToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(177, 6);
            // 
            // deleteToolStripMenuItem
            // 
            deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            deleteToolStripMenuItem.Size = new Size(180, 22);
            deleteToolStripMenuItem.Text = "Delete";
            deleteToolStripMenuItem.Click += deleteToolStripMenuItem_Click;
            // 
            // statusStripMain
            // 
            statusStripMain.Location = new Point(0, 539);
            statusStripMain.Name = "statusStripMain";
            statusStripMain.Padding = new Padding(1, 0, 16, 0);
            statusStripMain.Size = new Size(784, 22);
            statusStripMain.TabIndex = 2;
            statusStripMain.Text = "statusStrip1";
            // 
            // chartPerformance
            // 
            chartPerformance.Dock = DockStyle.Fill;
            chartPerformance.Location = new Point(0, 0);
            chartPerformance.Margin = new Padding(4, 3, 4, 3);
            chartPerformance.Name = "chartPerformance";
            chartPerformance.Size = new Size(784, 164);
            chartPerformance.TabIndex = 0;
            chartPerformance.Text = "chartPerformance";
            // 
            // splitContainerOuter
            // 
            splitContainerOuter.Dock = DockStyle.Fill;
            splitContainerOuter.FixedPanel = FixedPanel.Panel1;
            splitContainerOuter.Location = new Point(0, 24);
            splitContainerOuter.Name = "splitContainerOuter";
            splitContainerOuter.Orientation = Orientation.Horizontal;
            // 
            // splitContainerOuter.Panel1
            // 
            splitContainerOuter.Panel1.Controls.Add(chartPerformance);
            // 
            // splitContainerOuter.Panel2
            // 
            splitContainerOuter.Panel2.Controls.Add(splitContainerInner);
            splitContainerOuter.Size = new Size(784, 515);
            splitContainerOuter.SplitterDistance = 164;
            splitContainerOuter.TabIndex = 3;
            // 
            // splitContainerInner
            // 
            splitContainerInner.Dock = DockStyle.Fill;
            splitContainerInner.Location = new Point(0, 0);
            splitContainerInner.Name = "splitContainerInner";
            splitContainerInner.Orientation = Orientation.Horizontal;
            // 
            // splitContainerInner.Panel1
            // 
            splitContainerInner.Panel1.Controls.Add(dataGridViewProxys);
            // 
            // splitContainerInner.Panel2
            // 
            splitContainerInner.Panel2.Controls.Add(dataGridViewConnections);
            splitContainerInner.Size = new Size(784, 347);
            splitContainerInner.SplitterDistance = 193;
            splitContainerInner.TabIndex = 4;
            // 
            // dataGridViewConnections
            // 
            dataGridViewConnections.AllowUserToAddRows = false;
            dataGridViewConnections.AllowUserToDeleteRows = false;
            dataGridViewConnections.AllowUserToResizeRows = false;
            dataGridViewConnections.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewConnections.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewConnections.Columns.AddRange(new DataGridViewColumn[] { ColumnConnSourceAddress, ColumnConnSourceHost, ColumnConnSourcePort, ColumnConnDestinationAddress, ColumnConnDestinationHost, ColumnConnDestinationPort, ColumnConnProtocol, ColumnConnVersion, ColumnConnEncryption, ColumnConnBytesIn, ColumnConnBytesOut, ColumnConnInPerSecond, ColumnConnOutPerSecond, ColumnConnStarted, ColumnConnDuration, ColumnConnIdle });
            dataGridViewConnections.Dock = DockStyle.Fill;
            dataGridViewConnections.EditMode = DataGridViewEditMode.EditProgrammatically;
            dataGridViewConnections.Location = new Point(0, 0);
            dataGridViewConnections.Margin = new Padding(4, 3, 4, 3);
            dataGridViewConnections.MultiSelect = false;
            dataGridViewConnections.Name = "dataGridViewConnections";
            dataGridViewConnections.ReadOnly = true;
            dataGridViewConnections.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewConnections.Size = new Size(784, 150);
            dataGridViewConnections.TabIndex = 0;
            dataGridViewConnections.CellMouseUp += DataGridViewConnections_CellMouseUp;
            dataGridViewConnections.ColumnHeaderMouseClick += DataGridViewConnections_ColumnHeaderMouseClick;
            // 
            // ColumnConnSourceAddress
            // 
            ColumnConnSourceAddress.HeaderText = "Source IP";
            ColumnConnSourceAddress.Name = "ColumnConnSourceAddress";
            ColumnConnSourceAddress.ReadOnly = true;
            ColumnConnSourceAddress.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnSourceAddress.Width = 110;
            // 
            // ColumnConnSourceHost
            // 
            ColumnConnSourceHost.HeaderText = "Source Host";
            ColumnConnSourceHost.Name = "ColumnConnSourceHost";
            ColumnConnSourceHost.ReadOnly = true;
            ColumnConnSourceHost.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnSourceHost.Width = 130;
            // 
            // ColumnConnSourcePort
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleRight;
            ColumnConnSourcePort.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnSourcePort.HeaderText = "Source Port";
            ColumnConnSourcePort.Name = "ColumnConnSourcePort";
            ColumnConnSourcePort.ReadOnly = true;
            ColumnConnSourcePort.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnSourcePort.Width = 95;
            // 
            // ColumnConnDestinationAddress
            // 
            ColumnConnDestinationAddress.HeaderText = "Destination IP";
            ColumnConnDestinationAddress.Name = "ColumnConnDestinationAddress";
            ColumnConnDestinationAddress.ReadOnly = true;
            ColumnConnDestinationAddress.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnDestinationAddress.Width = 110;
            // 
            // ColumnConnDestinationHost
            // 
            ColumnConnDestinationHost.HeaderText = "Destination Host";
            ColumnConnDestinationHost.Name = "ColumnConnDestinationHost";
            ColumnConnDestinationHost.ReadOnly = true;
            ColumnConnDestinationHost.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnDestinationHost.Width = 130;
            // 
            // ColumnConnDestinationPort
            // 
            ColumnConnDestinationPort.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnDestinationPort.HeaderText = "Destination Port";
            ColumnConnDestinationPort.Name = "ColumnConnDestinationPort";
            ColumnConnDestinationPort.ReadOnly = true;
            ColumnConnDestinationPort.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnDestinationPort.Width = 125;
            // 
            // ColumnConnProtocol
            // 
            ColumnConnProtocol.HeaderText = "Protocol";
            ColumnConnProtocol.Name = "ColumnConnProtocol";
            ColumnConnProtocol.ReadOnly = true;
            ColumnConnProtocol.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnProtocol.Width = 70;
            // 
            // ColumnConnVersion
            // 
            ColumnConnVersion.HeaderText = "Version";
            ColumnConnVersion.Name = "ColumnConnVersion";
            ColumnConnVersion.ReadOnly = true;
            ColumnConnVersion.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnVersion.Width = 70;
            // 
            // ColumnConnEncryption
            // 
            ColumnConnEncryption.HeaderText = "Encryption";
            ColumnConnEncryption.Name = "ColumnConnEncryption";
            ColumnConnEncryption.ReadOnly = true;
            ColumnConnEncryption.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnEncryption.Width = 130;
            // 
            // ColumnConnBytesIn
            // 
            ColumnConnBytesIn.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnBytesIn.HeaderText = "Bytes In";
            ColumnConnBytesIn.Name = "ColumnConnBytesIn";
            ColumnConnBytesIn.ReadOnly = true;
            ColumnConnBytesIn.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnBytesIn.Width = 80;
            // 
            // ColumnConnBytesOut
            // 
            ColumnConnBytesOut.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnBytesOut.HeaderText = "Bytes Out";
            ColumnConnBytesOut.Name = "ColumnConnBytesOut";
            ColumnConnBytesOut.ReadOnly = true;
            ColumnConnBytesOut.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnBytesOut.Width = 80;
            // 
            // ColumnConnInPerSecond
            // 
            ColumnConnInPerSecond.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnInPerSecond.HeaderText = "In/s";
            ColumnConnInPerSecond.Name = "ColumnConnInPerSecond";
            ColumnConnInPerSecond.ReadOnly = true;
            ColumnConnInPerSecond.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnInPerSecond.Width = 80;
            // 
            // ColumnConnOutPerSecond
            // 
            ColumnConnOutPerSecond.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnOutPerSecond.HeaderText = "Out/s";
            ColumnConnOutPerSecond.Name = "ColumnConnOutPerSecond";
            ColumnConnOutPerSecond.ReadOnly = true;
            ColumnConnOutPerSecond.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnOutPerSecond.Width = 80;
            // 
            // ColumnConnStarted
            // 
            ColumnConnStarted.HeaderText = "Started";
            ColumnConnStarted.Name = "ColumnConnStarted";
            ColumnConnStarted.ReadOnly = true;
            ColumnConnStarted.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnStarted.Width = 140;
            // 
            // ColumnConnDuration
            // 
            ColumnConnDuration.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnDuration.HeaderText = "Duration";
            ColumnConnDuration.Name = "ColumnConnDuration";
            ColumnConnDuration.ReadOnly = true;
            ColumnConnDuration.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnDuration.Width = 80;
            // 
            // ColumnConnIdle
            // 
            ColumnConnIdle.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnConnIdle.HeaderText = "Idle";
            ColumnConnIdle.Name = "ColumnConnIdle";
            ColumnConnIdle.ReadOnly = true;
            ColumnConnIdle.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnConnIdle.Width = 70;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 561);
            Controls.Add(splitContainerOuter);
            Controls.Add(statusStripMain);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Margin = new Padding(4, 3, 4, 3);
            MinimumSize = new Size(600, 400);
            Name = "FormMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "NetProxy";
            FormClosed += FormMain_FormClosed;
            Shown += FormMain_Shown;
            ((System.ComponentModel.ISupportInitialize)dataGridViewProxys).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)chartPerformance).EndInit();
            splitContainerOuter.Panel1.ResumeLayout(false);
            splitContainerOuter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerOuter).EndInit();
            splitContainerOuter.ResumeLayout(false);
            splitContainerInner.Panel1.ResumeLayout(false);
            splitContainerInner.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerInner).EndInit();
            splitContainerInner.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewConnections).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dataGridViewProxys;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem changeConnectionToolStripMenuItem;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem pluginsToolStripMenuItem;
        private ToolStripMenuItem certificatesToolStripMenuItem;
        private ToolStripMenuItem configurationToolStripMenuItem;
        private ToolStripMenuItem languageToolStripMenuItem;
        private StatusStrip statusStripMain;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartPerformance;
        private DataGridViewImageColumn ColumnStatus;
        private DataGridViewTextBoxColumn ColumnId;
        private DataGridViewTextBoxColumn ColumnName;
        private DataGridViewTextBoxColumn ColumnProxyType;
        private DataGridViewTextBoxColumn ColummListenPort;
        private DataGridViewTextBoxColumn ColumnBytesTransferred;
        private DataGridViewTextBoxColumn ColumnTotalConnections;
        private DataGridViewTextBoxColumn ColumnCurrentConnections;
        private SplitContainer splitContainerOuter;
        private SplitContainer splitContainerInner;
        private DataGridView dataGridViewConnections;
        private DataGridViewTextBoxColumn ColumnConnSourceAddress;
        private DataGridViewTextBoxColumn ColumnConnSourceHost;
        private DataGridViewTextBoxColumn ColumnConnSourcePort;
        private DataGridViewTextBoxColumn ColumnConnDestinationAddress;
        private DataGridViewTextBoxColumn ColumnConnDestinationHost;
        private DataGridViewTextBoxColumn ColumnConnDestinationPort;
        private DataGridViewTextBoxColumn ColumnConnProtocol;
        private DataGridViewTextBoxColumn ColumnConnVersion;
        private DataGridViewTextBoxColumn ColumnConnEncryption;
        private DataGridViewTextBoxColumn ColumnConnBytesIn;
        private DataGridViewTextBoxColumn ColumnConnBytesOut;
        private DataGridViewTextBoxColumn ColumnConnInPerSecond;
        private DataGridViewTextBoxColumn ColumnConnOutPerSecond;
        private DataGridViewTextBoxColumn ColumnConnStarted;
        private DataGridViewTextBoxColumn ColumnConnDuration;
        private DataGridViewTextBoxColumn ColumnConnIdle;
        private ToolStripMenuItem proxyToolStripMenuItem;
        private ToolStripMenuItem addToolStripMenuItem;
        private ToolStripMenuItem refreshToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem startToolStripMenuItem;
        private ToolStripMenuItem stopToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem deleteToolStripMenuItem;
    }
}