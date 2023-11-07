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
            buttonSave = new Button();
            buttonCancel = new Button();
            tabControl1.SuspendLayout();
            tabPageUsers.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewUsers).BeginInit();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPageUsers);
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
            tabPageUsers.Margin = new Padding(4, 3, 4, 3);
            tabPageUsers.Name = "tabPageUsers";
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
            dataGridViewUsers.Location = new Point(0, 0);
            dataGridViewUsers.Margin = new Padding(4, 3, 4, 3);
            dataGridViewUsers.MultiSelect = false;
            dataGridViewUsers.Name = "dataGridViewUsers";
            dataGridViewUsers.Size = new Size(450, 380);
            dataGridViewUsers.TabIndex = 0;
            dataGridViewUsers.CellClick += dataGridViewUsers_CellClick;
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
            // buttonSave
            // 
            buttonSave.Location = new Point(290, 429);
            buttonSave.Margin = new Padding(4, 3, 4, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(88, 27);
            buttonSave.TabIndex = 1;
            buttonSave.Text = "Save";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += buttonSave_Click;
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
            buttonCancel.Click += buttonCancel_Click;
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
    }
}