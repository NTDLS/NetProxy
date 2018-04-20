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
            this.dataGridViewRoutes = new System.Windows.Forms.DataGridView();
            this.ColumnStatus = new System.Windows.Forms.DataGridViewImageColumn();
            this.ColumnId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnRouterType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColummListenPort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnBytesTransferred = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnTotalConnections = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnCurrentConnections = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeConnectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.configurationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStripMain = new System.Windows.Forms.StatusStrip();
            this.panelPerformance = new System.Windows.Forms.Panel();
            this.chartPerformance = new System.Windows.Forms.DataVisualization.Charting.Chart();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRoutes)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.panelPerformance.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chartPerformance)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridViewRoutes
            // 
            this.dataGridViewRoutes.AllowUserToAddRows = false;
            this.dataGridViewRoutes.AllowUserToDeleteRows = false;
            this.dataGridViewRoutes.AllowUserToResizeRows = false;
            this.dataGridViewRoutes.BackgroundColor = System.Drawing.SystemColors.ButtonFace;
            this.dataGridViewRoutes.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewRoutes.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnStatus,
            this.ColumnId,
            this.ColumnName,
            this.ColumnRouterType,
            this.ColummListenPort,
            this.ColumnBytesTransferred,
            this.ColumnTotalConnections,
            this.ColumnCurrentConnections});
            this.dataGridViewRoutes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewRoutes.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dataGridViewRoutes.Location = new System.Drawing.Point(0, 124);
            this.dataGridViewRoutes.MultiSelect = false;
            this.dataGridViewRoutes.Name = "dataGridViewRoutes";
            this.dataGridViewRoutes.ReadOnly = true;
            this.dataGridViewRoutes.Size = new System.Drawing.Size(650, 246);
            this.dataGridViewRoutes.TabIndex = 0;
            this.dataGridViewRoutes.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewRoutes_CellDoubleClick);
            this.dataGridViewRoutes.MouseDown += new System.Windows.Forms.MouseEventHandler(this.dataGridViewRoutes_MouseDown);
            // 
            // ColumnStatus
            // 
            this.ColumnStatus.Frozen = true;
            this.ColumnStatus.HeaderText = "Status";
            this.ColumnStatus.Name = "ColumnStatus";
            this.ColumnStatus.ReadOnly = true;
            this.ColumnStatus.Width = 45;
            // 
            // ColumnId
            // 
            this.ColumnId.DataPropertyName = "Id";
            this.ColumnId.HeaderText = "Id";
            this.ColumnId.Name = "ColumnId";
            this.ColumnId.ReadOnly = true;
            this.ColumnId.Visible = false;
            // 
            // ColumnName
            // 
            this.ColumnName.DataPropertyName = "Name";
            this.ColumnName.HeaderText = "Name";
            this.ColumnName.Name = "ColumnName";
            this.ColumnName.ReadOnly = true;
            this.ColumnName.Width = 200;
            // 
            // ColumnRouterType
            // 
            this.ColumnRouterType.DataPropertyName = "RouterType";
            this.ColumnRouterType.HeaderText = "Type";
            this.ColumnRouterType.Name = "ColumnRouterType";
            this.ColumnRouterType.ReadOnly = true;
            this.ColumnRouterType.Width = 80;
            // 
            // ColummListenPort
            // 
            this.ColummListenPort.DataPropertyName = "ListenPort";
            this.ColummListenPort.HeaderText = "Port";
            this.ColummListenPort.Name = "ColummListenPort";
            this.ColummListenPort.ReadOnly = true;
            this.ColummListenPort.Width = 50;
            // 
            // ColumnBytesTransferred
            // 
            this.ColumnBytesTransferred.HeaderText = "Bytes Transferred";
            this.ColumnBytesTransferred.Name = "ColumnBytesTransferred";
            this.ColumnBytesTransferred.ReadOnly = true;
            this.ColumnBytesTransferred.Width = 80;
            // 
            // ColumnTotalConnections
            // 
            this.ColumnTotalConnections.HeaderText = "Total Conn.";
            this.ColumnTotalConnections.Name = "ColumnTotalConnections";
            this.ColumnTotalConnections.ReadOnly = true;
            this.ColumnTotalConnections.Width = 70;
            // 
            // ColumnCurrentConnections
            // 
            this.ColumnCurrentConnections.HeaderText = "Curr. Conn.";
            this.ColumnCurrentConnections.Name = "ColumnCurrentConnections";
            this.ColumnCurrentConnections.ReadOnly = true;
            this.ColumnCurrentConnections.Width = 50;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(650, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.changeConnectionToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // changeConnectionToolStripMenuItem
            // 
            this.changeConnectionToolStripMenuItem.Name = "changeConnectionToolStripMenuItem";
            this.changeConnectionToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.changeConnectionToolStripMenuItem.Text = "Change Connection";
            this.changeConnectionToolStripMenuItem.Click += new System.EventHandler(this.changeConnectionToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.configurationToolStripMenuItem});
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            this.settingsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.settingsToolStripMenuItem.Text = "Settings";
            // 
            // configurationToolStripMenuItem
            // 
            this.configurationToolStripMenuItem.Name = "configurationToolStripMenuItem";
            this.configurationToolStripMenuItem.Size = new System.Drawing.Size(148, 22);
            this.configurationToolStripMenuItem.Text = "Configuration";
            this.configurationToolStripMenuItem.Click += new System.EventHandler(this.configurationToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // statusStripMain
            // 
            this.statusStripMain.Location = new System.Drawing.Point(0, 370);
            this.statusStripMain.Name = "statusStripMain";
            this.statusStripMain.Size = new System.Drawing.Size(650, 22);
            this.statusStripMain.TabIndex = 2;
            this.statusStripMain.Text = "statusStrip1";
            // 
            // panelPerformance
            // 
            this.panelPerformance.Controls.Add(this.chartPerformance);
            this.panelPerformance.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPerformance.Location = new System.Drawing.Point(0, 24);
            this.panelPerformance.Name = "panelPerformance";
            this.panelPerformance.Size = new System.Drawing.Size(650, 100);
            this.panelPerformance.TabIndex = 3;
            // 
            // chartPerformance
            // 
            this.chartPerformance.BackColor = System.Drawing.Color.WhiteSmoke;
            this.chartPerformance.BackSecondaryColor = System.Drawing.Color.White;
            chartArea1.AxisX.LabelStyle.Enabled = false;
            chartArea1.AxisX.MajorGrid.Enabled = false;
            chartArea1.BackColor = System.Drawing.Color.WhiteSmoke;
            chartArea1.Name = "ChartArea1";
            this.chartPerformance.ChartAreas.Add(chartArea1);
            this.chartPerformance.Dock = System.Windows.Forms.DockStyle.Fill;
            legend1.BackColor = System.Drawing.Color.WhiteSmoke;
            legend1.Name = "Legend1";
            this.chartPerformance.Legends.Add(legend1);
            this.chartPerformance.Location = new System.Drawing.Point(0, 0);
            this.chartPerformance.Name = "chartPerformance";
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.StepLine;
            series1.Color = System.Drawing.Color.Red;
            series1.Legend = "Legend1";
            series1.Name = "MB/s Sent";
            series2.ChartArea = "ChartArea1";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.StepLine;
            series2.Color = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(192)))), ((int)(((byte)(0)))));
            series2.Legend = "Legend1";
            series2.Name = "MB/s Recv";
            series3.ChartArea = "ChartArea1";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.StepLine;
            series3.Color = System.Drawing.Color.Blue;
            series3.Legend = "Legend1";
            series3.Name = "Connections";
            this.chartPerformance.Series.Add(series1);
            this.chartPerformance.Series.Add(series2);
            this.chartPerformance.Series.Add(series3);
            this.chartPerformance.Size = new System.Drawing.Size(650, 100);
            this.chartPerformance.TabIndex = 0;
            this.chartPerformance.Text = "chart1";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(650, 392);
            this.Controls.Add(this.dataGridViewRoutes);
            this.Controls.Add(this.statusStripMain);
            this.Controls.Add(this.panelPerformance);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NetProxy";
            this.Shown += new System.EventHandler(this.FormMain_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRoutes)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.panelPerformance.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.chartPerformance)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridViewRoutes;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem changeConnectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem configurationToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStripMain;
        private System.Windows.Forms.Panel panelPerformance;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartPerformance;
        private System.Windows.Forms.DataGridViewImageColumn ColumnStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnId;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnRouterType;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColummListenPort;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnBytesTransferred;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnTotalConnections;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnCurrentConnections;
    }
}