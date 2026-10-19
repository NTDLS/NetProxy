namespace NetProxy.Client.Forms
{
    partial class FormServerSettings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormServerSettings));
            tabControl1 = new TabControl();
            tabPageUsers = new TabPage();
            dataGridViewUsers = new DataGridView();
            ColumnId = new DataGridViewTextBoxColumn();
            ColumnUsername = new DataGridViewTextBoxColumn();
            ColumnSetPassword = new DataGridViewLinkColumn();
            ColumnDescription = new DataGridViewTextBoxColumn();
            ColumnPassword = new DataGridViewTextBoxColumn();
            tabPageService = new TabPage();
            groupBoxLogging = new GroupBox();
            labelLogLevel = new Label();
            comboBoxLogLevel = new ComboBox();
            labelLogPath = new Label();
            textBoxLogPath = new TextBox();
            labelLogFileMaxSizeMB = new Label();
            textBoxLogFileMaxSizeMB = new TextBox();
            labelLogFileRetainedCount = new Label();
            textBoxLogFileRetainedCount = new TextBox();
            checkBoxLogToEventLog = new CheckBox();
            checkBoxManagementListenOnIPv6 = new CheckBox();
            buttonSave = new Button();
            buttonCancel = new Button();
            tabControl1.SuspendLayout();
            tabPageUsers.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewUsers).BeginInit();
            tabPageService.SuspendLayout();
            groupBoxLogging.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPageUsers);
            tabControl1.Controls.Add(tabPageService);
            tabControl1.Location = new Point(14, 14);
            tabControl1.Margin = new Padding(4, 3, 4, 3);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(458, 408);
            tabControl1.TabIndex = 0;
            // 
            // tabPageUsers
            // 
            tabPageUsers.Controls.Add(dataGridViewUsers);
            tabPageUsers.Location = new Point(4, 24);
            tabPageUsers.Name = "tabPageUsers";
            tabPageUsers.Padding = new Padding(3);
            tabPageUsers.Size = new Size(450, 380);
            tabPageUsers.TabIndex = 0;
            tabPageUsers.Text = "Users";
            tabPageUsers.UseVisualStyleBackColor = true;
            // 
            // dataGridViewUsers
            // 
            dataGridViewUsers.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewUsers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewUsers.Columns.AddRange(new DataGridViewColumn[] { ColumnId, ColumnUsername, ColumnSetPassword, ColumnDescription, ColumnPassword });
            dataGridViewUsers.Dock = DockStyle.Fill;
            dataGridViewUsers.Location = new Point(3, 3);
            dataGridViewUsers.Margin = new Padding(4, 3, 4, 3);
            dataGridViewUsers.MultiSelect = false;
            dataGridViewUsers.Name = "dataGridViewUsers";
            dataGridViewUsers.Size = new Size(444, 374);
            dataGridViewUsers.TabIndex = 0;
            dataGridViewUsers.CellClick += DataGridViewUsers_CellClick;
            // 
            // ColumnId
            // 
            ColumnId.HeaderText = "Id";
            ColumnId.MinimumWidth = 50;
            ColumnId.Name = "ColumnId";
            ColumnId.Visible = false;
            // 
            // ColumnUsername
            // 
            ColumnUsername.HeaderText = "Username";
            ColumnUsername.Name = "ColumnUsername";
            // 
            // ColumnSetPassword
            // 
            dataGridViewCellStyle1.NullValue = "Set Password";
            ColumnSetPassword.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnSetPassword.HeaderText = "Set Password";
            ColumnSetPassword.Name = "ColumnSetPassword";
            // 
            // ColumnDescription
            // 
            ColumnDescription.HeaderText = "Description";
            ColumnDescription.Name = "ColumnDescription";
            // 
            // ColumnPassword
            // 
            ColumnPassword.HeaderText = "ColumnPassword";
            ColumnPassword.Name = "ColumnPassword";
            ColumnPassword.Visible = false;
            // 
            // tabPageService
            // 
            tabPageService.Controls.Add(checkBoxManagementListenOnIPv6);
            tabPageService.Controls.Add(groupBoxLogging);
            tabPageService.Location = new Point(4, 24);
            tabPageService.Name = "tabPageService";
            tabPageService.Padding = new Padding(3);
            tabPageService.Size = new Size(450, 380);
            tabPageService.TabIndex = 1;
            tabPageService.Text = "Service";
            tabPageService.UseVisualStyleBackColor = true;
            // 
            // groupBoxLogging
            // 
            groupBoxLogging.Controls.Add(labelLogLevel);
            groupBoxLogging.Controls.Add(comboBoxLogLevel);
            groupBoxLogging.Controls.Add(labelLogPath);
            groupBoxLogging.Controls.Add(textBoxLogPath);
            groupBoxLogging.Controls.Add(labelLogFileMaxSizeMB);
            groupBoxLogging.Controls.Add(textBoxLogFileMaxSizeMB);
            groupBoxLogging.Controls.Add(labelLogFileRetainedCount);
            groupBoxLogging.Controls.Add(textBoxLogFileRetainedCount);
            groupBoxLogging.Controls.Add(checkBoxLogToEventLog);
            groupBoxLogging.Location = new Point(6, 6);
            groupBoxLogging.Name = "groupBoxLogging";
            groupBoxLogging.Size = new Size(430, 190);
            groupBoxLogging.TabIndex = 0;
            groupBoxLogging.TabStop = false;
            groupBoxLogging.Text = "Logging (takes effect immediately)";
            // 
            // labelLogLevel
            // 
            labelLogLevel.AutoSize = true;
            labelLogLevel.Location = new Point(6, 19);
            labelLogLevel.Name = "labelLogLevel";
            labelLogLevel.Size = new Size(54, 15);
            labelLogLevel.TabIndex = 0;
            labelLogLevel.Text = "Log level";
            // 
            // comboBoxLogLevel
            // 
            comboBoxLogLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxLogLevel.FormattingEnabled = true;
            comboBoxLogLevel.Location = new Point(8, 37);
            comboBoxLogLevel.Name = "comboBoxLogLevel";
            comboBoxLogLevel.Size = new Size(140, 23);
            comboBoxLogLevel.TabIndex = 1;
            // 
            // labelLogPath
            // 
            labelLogPath.AutoSize = true;
            labelLogPath.Location = new Point(6, 65);
            labelLogPath.Name = "labelLogPath";
            labelLogPath.Size = new Size(273, 15);
            labelLogPath.TabIndex = 2;
            labelLogPath.Text = "Log file (on the service machine, empty for no file)";
            // 
            // textBoxLogPath
            // 
            textBoxLogPath.Location = new Point(8, 83);
            textBoxLogPath.Name = "textBoxLogPath";
            textBoxLogPath.Size = new Size(400, 23);
            textBoxLogPath.TabIndex = 3;
            // 
            // labelLogFileMaxSizeMB
            // 
            labelLogFileMaxSizeMB.AutoSize = true;
            labelLogFileMaxSizeMB.Location = new Point(8, 111);
            labelLogFileMaxSizeMB.Name = "labelLogFileMaxSizeMB";
            labelLogFileMaxSizeMB.Size = new Size(134, 15);
            labelLogFileMaxSizeMB.TabIndex = 4;
            labelLogFileMaxSizeMB.Text = "Roll the file over at (MB)";
            // 
            // textBoxLogFileMaxSizeMB
            // 
            textBoxLogFileMaxSizeMB.Location = new Point(8, 129);
            textBoxLogFileMaxSizeMB.Name = "textBoxLogFileMaxSizeMB";
            textBoxLogFileMaxSizeMB.Size = new Size(116, 23);
            textBoxLogFileMaxSizeMB.TabIndex = 5;
            // 
            // labelLogFileRetainedCount
            // 
            labelLogFileRetainedCount.AutoSize = true;
            labelLogFileRetainedCount.Location = new Point(210, 111);
            labelLogFileRetainedCount.Name = "labelLogFileRetainedCount";
            labelLogFileRetainedCount.Size = new Size(106, 15);
            labelLogFileRetainedCount.TabIndex = 6;
            labelLogFileRetainedCount.Text = "Rolled files to keep";
            // 
            // textBoxLogFileRetainedCount
            // 
            textBoxLogFileRetainedCount.Location = new Point(210, 129);
            textBoxLogFileRetainedCount.Name = "textBoxLogFileRetainedCount";
            textBoxLogFileRetainedCount.Size = new Size(116, 23);
            textBoxLogFileRetainedCount.TabIndex = 7;
            // 
            // checkBoxLogToEventLog
            // 
            checkBoxLogToEventLog.AutoSize = true;
            checkBoxLogToEventLog.Location = new Point(8, 160);
            checkBoxLogToEventLog.Name = "checkBoxLogToEventLog";
            checkBoxLogToEventLog.Size = new Size(349, 19);
            checkBoxLogToEventLog.TabIndex = 8;
            checkBoxLogToEventLog.Text = "Also write warnings and exceptions to the Windows event log";
            checkBoxLogToEventLog.UseVisualStyleBackColor = true;
            // 
            // checkBoxManagementListenOnIPv6
            // 
            checkBoxManagementListenOnIPv6.AutoSize = true;
            checkBoxManagementListenOnIPv6.Location = new Point(6, 202);
            checkBoxManagementListenOnIPv6.Name = "checkBoxManagementListenOnIPv6";
            checkBoxManagementListenOnIPv6.Size = new Size(233, 19);
            checkBoxManagementListenOnIPv6.TabIndex = 0;
            checkBoxManagementListenOnIPv6.Text = "Accept IPv6 connections as well as IPv4";
            checkBoxManagementListenOnIPv6.UseVisualStyleBackColor = true;
            // 
            // buttonSave
            // 
            buttonSave.Location = new Point(290, 429);
            buttonSave.Margin = new Padding(4, 3, 4, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(88, 27);
            buttonSave.TabIndex = 1;
            buttonSave.Text = "Save";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(385, 429);
            buttonCancel.Margin = new Padding(4, 3, 4, 3);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(88, 27);
            buttonCancel.TabIndex = 2;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // FormServerSettings
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(486, 468);
            Controls.Add(buttonCancel);
            Controls.Add(buttonSave);
            Controls.Add(tabControl1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormServerSettings";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Server Settings";
            FormClosed += FormServerSettings_FormClosed;
            Shown += FormServerSettings_Shown;
            tabControl1.ResumeLayout(false);
            tabPageUsers.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewUsers).EndInit();
            tabPageService.ResumeLayout(false);
            tabPageService.PerformLayout();
            groupBoxLogging.ResumeLayout(false);
            groupBoxLogging.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl1;
        private Button buttonSave;
        private Button buttonCancel;
        private TabPage tabPageUsers;
        private DataGridView dataGridViewUsers;
        private DataGridViewTextBoxColumn ColumnId;
        private DataGridViewTextBoxColumn ColumnUsername;
        private DataGridViewLinkColumn ColumnSetPassword;
        private DataGridViewTextBoxColumn ColumnDescription;
        private DataGridViewTextBoxColumn ColumnPassword;
        private TabPage tabPageService;
        private GroupBox groupBoxLogging;
        private Label labelLogLevel;
        private ComboBox comboBoxLogLevel;
        private Label labelLogPath;
        private TextBox textBoxLogPath;
        private Label labelLogFileMaxSizeMB;
        private TextBox textBoxLogFileMaxSizeMB;
        private Label labelLogFileRetainedCount;
        private TextBox textBoxLogFileRetainedCount;
        private CheckBox checkBoxLogToEventLog;
        private CheckBox checkBoxManagementListenOnIPv6;
    }
}