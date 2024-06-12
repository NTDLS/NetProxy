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
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series3 = new System.Windows.Forms.DataVisualization.Charting.Series();
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
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            statusStripMain = new StatusStrip();
            panelPerformance = new Panel();
            chartPerformance = new System.Windows.Forms.DataVisualization.Charting.Chart();
            ((System.ComponentModel.ISupportInitialize)dataGridViewProxys).BeginInit();
            menuStrip1.SuspendLayout();
            panelPerformance.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)chartPerformance).BeginInit();
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
            dataGridViewProxys.Location = new Point(0, 139);
            dataGridViewProxys.Margin = new Padding(4, 3, 4, 3);
            dataGridViewProxys.MultiSelect = false;
            dataGridViewProxys.Name = "dataGridViewProxys";
            dataGridViewProxys.ReadOnly = true;
            dataGridViewProxys.Size = new Size(758, 291);
            dataGridViewProxys.TabIndex = 0;
            dataGridViewProxys.CellDoubleClick += dataGridViewProxys_CellDoubleClick;
            dataGridViewProxys.MouseDown += dataGridViewProxys_MouseDown;
            // 
            // ColumnStatus
            // 
            ColumnStatus.Frozen = true;
            ColumnStatus.HeaderText = "Status";
            ColumnStatus.Name = "ColumnStatus";
            ColumnStatus.ReadOnly = true;
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
            ColumnName.DataPropertyName = "Name";
            ColumnName.HeaderText = "Name";
            ColumnName.Name = "ColumnName";
            ColumnName.ReadOnly = true;
            ColumnName.Width = 200;
            // 
            // ColumnProxyType
            // 
            ColumnProxyType.DataPropertyName = "ProxyType";
            ColumnProxyType.HeaderText = "Type";
            ColumnProxyType.Name = "ColumnProxyType";
            ColumnProxyType.ReadOnly = true;
            ColumnProxyType.Width = 80;
            // 
            // ColummListenPort
            // 
            ColummListenPort.DataPropertyName = "ListenPort";
            ColummListenPort.HeaderText = "Port";
            ColummListenPort.Name = "ColummListenPort";
            ColummListenPort.ReadOnly = true;
            ColummListenPort.Width = 50;
            // 
            // ColumnBytesTransferred
            // 
            ColumnBytesTransferred.HeaderText = "Bytes Transferred";
            ColumnBytesTransferred.Name = "ColumnBytesTransferred";
            ColumnBytesTransferred.ReadOnly = true;
            ColumnBytesTransferred.Width = 80;
            // 
            // ColumnTotalConnections
            // 
            ColumnTotalConnections.HeaderText = "Total Conn.";
            ColumnTotalConnections.Name = "ColumnTotalConnections";
            ColumnTotalConnections.ReadOnly = true;
            ColumnTotalConnections.Width = 70;
            // 
            // ColumnCurrentConnections
            // 
            ColumnCurrentConnections.HeaderText = "Curr. Conn.";
            ColumnCurrentConnections.Name = "ColumnCurrentConnections";
            ColumnCurrentConnections.ReadOnly = true;
            ColumnCurrentConnections.Width = 50;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, settingsToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(7, 2, 0, 2);
            menuStrip1.Size = new Size(758, 24);
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
            changeConnectionToolStripMenuItem.Click += changeConnectionToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(180, 22);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { configurationToolStripMenuItem });
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(61, 20);
            settingsToolStripMenuItem.Text = "Settings";
            // 
            // configurationToolStripMenuItem
            // 
            configurationToolStripMenuItem.Name = "configurationToolStripMenuItem";
            configurationToolStripMenuItem.Size = new Size(148, 22);
            configurationToolStripMenuItem.Text = "Configuration";
            configurationToolStripMenuItem.Click += configurationToolStripMenuItem_Click;
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
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // statusStripMain
            // 
            statusStripMain.Location = new Point(0, 430);
            statusStripMain.Name = "statusStripMain";
            statusStripMain.Padding = new Padding(1, 0, 16, 0);
            statusStripMain.Size = new Size(758, 22);
            statusStripMain.TabIndex = 2;
            statusStripMain.Text = "statusStrip1";
            // 
            // panelPerformance
            // 
            panelPerformance.Controls.Add(chartPerformance);
            panelPerformance.Dock = DockStyle.Top;
            panelPerformance.Location = new Point(0, 24);
            panelPerformance.Margin = new Padding(4, 3, 4, 3);
            panelPerformance.Name = "panelPerformance";
            panelPerformance.Size = new Size(758, 115);
            panelPerformance.TabIndex = 3;
            // 
            // chartPerformance
            // 
            chartPerformance.BackColor = Color.WhiteSmoke;
            chartPerformance.BackSecondaryColor = Color.White;
            chartArea1.AxisX.LabelStyle.Enabled = false;
            chartArea1.AxisX.MajorGrid.Enabled = false;
            chartArea1.BackColor = Color.WhiteSmoke;
            chartArea1.Name = "ChartArea1";
            chartPerformance.ChartAreas.Add(chartArea1);
            chartPerformance.Dock = DockStyle.Fill;
            legend1.BackColor = Color.WhiteSmoke;
            legend1.Name = "Legend1";
            chartPerformance.Legends.Add(legend1);
            chartPerformance.Location = new Point(0, 0);
            chartPerformance.Margin = new Padding(4, 3, 4, 3);
            chartPerformance.Name = "chartPerformance";
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            series1.Color = Color.Red;
            series1.Legend = "Legend1";
            series1.Name = "KB/s Sent";
            series2.ChartArea = "ChartArea1";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            series2.Color = Color.FromArgb(0, 192, 0);
            series2.Legend = "Legend1";
            series2.Name = "KB/s Recv";
            series3.ChartArea = "ChartArea1";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            series3.Color = Color.Blue;
            series3.Legend = "Legend1";
            series3.Name = "Connections";
            chartPerformance.Series.Add(series1);
            chartPerformance.Series.Add(series2);
            chartPerformance.Series.Add(series3);
            chartPerformance.Size = new Size(758, 115);
            chartPerformance.TabIndex = 0;
            chartPerformance.Text = "chart1";
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(758, 452);
            Controls.Add(dataGridViewProxys);
            Controls.Add(statusStripMain);
            Controls.Add(panelPerformance);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Margin = new Padding(4, 3, 4, 3);
            Name = "FormMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "NetProxy";
            FormClosed += FormMain_FormClosed;
            Shown += FormMain_Shown;
            ((System.ComponentModel.ISupportInitialize)dataGridViewProxys).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            panelPerformance.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)chartPerformance).EndInit();
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
        private ToolStripMenuItem configurationToolStripMenuItem;
        private StatusStrip statusStripMain;
        private Panel panelPerformance;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartPerformance;
        private DataGridViewImageColumn ColumnStatus;
        private DataGridViewTextBoxColumn ColumnId;
        private DataGridViewTextBoxColumn ColumnName;
        private DataGridViewTextBoxColumn ColumnProxyType;
        private DataGridViewTextBoxColumn ColummListenPort;
        private DataGridViewTextBoxColumn ColumnBytesTransferred;
        private DataGridViewTextBoxColumn ColumnTotalConnections;
        private DataGridViewTextBoxColumn ColumnCurrentConnections;
    }
}