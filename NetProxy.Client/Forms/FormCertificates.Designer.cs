namespace NetProxy.Client.Forms
{
    partial class FormCertificates
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
            dataGridViewCertificates = new DataGridView();
            ColumnName = new DataGridViewTextBoxColumn();
            ColumnExpires = new DataGridViewTextBoxColumn();
            ColumnDaysLeft = new DataGridViewTextBoxColumn();
            ColumnUsedBy = new DataGridViewTextBoxColumn();
            ColumnFile = new DataGridViewTextBoxColumn();
            ColumnSubject = new DataGridViewTextBoxColumn();
            buttonAdd = new Button();
            buttonEdit = new Button();
            buttonDelete = new Button();
            buttonRefresh = new Button();
            buttonClose = new Button();
            labelInfo = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridViewCertificates).BeginInit();
            SuspendLayout();
            //
            // dataGridViewCellStyle1
            //
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleRight;
            //
            // dataGridViewCertificates
            //
            dataGridViewCertificates.AllowUserToAddRows = false;
            dataGridViewCertificates.AllowUserToDeleteRows = false;
            dataGridViewCertificates.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewCertificates.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCertificates.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewCertificates.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCertificates.Columns.AddRange(new DataGridViewColumn[] { ColumnName, ColumnExpires, ColumnDaysLeft, ColumnUsedBy, ColumnFile, ColumnSubject });
            dataGridViewCertificates.Location = new Point(12, 12);
            dataGridViewCertificates.MultiSelect = false;
            dataGridViewCertificates.Name = "dataGridViewCertificates";
            dataGridViewCertificates.ReadOnly = true;
            dataGridViewCertificates.RowHeadersVisible = false;
            dataGridViewCertificates.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewCertificates.Size = new Size(856, 330);
            dataGridViewCertificates.TabIndex = 0;
            dataGridViewCertificates.CellDoubleClick += DataGridViewCertificates_CellDoubleClick;
            //
            // ColumnName
            //
            ColumnName.FillWeight = 16F;
            ColumnName.HeaderText = "Name";
            ColumnName.Name = "ColumnName";
            ColumnName.ReadOnly = true;
            //
            // ColumnExpires
            //
            ColumnExpires.FillWeight = 11F;
            ColumnExpires.HeaderText = "Expires";
            ColumnExpires.Name = "ColumnExpires";
            ColumnExpires.ReadOnly = true;
            //
            // ColumnDaysLeft
            //
            ColumnDaysLeft.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnDaysLeft.FillWeight = 8F;
            ColumnDaysLeft.HeaderText = "Days left";
            ColumnDaysLeft.Name = "ColumnDaysLeft";
            ColumnDaysLeft.ReadOnly = true;
            //
            // ColumnUsedBy
            //
            ColumnUsedBy.FillWeight = 20F;
            ColumnUsedBy.HeaderText = "Used by";
            ColumnUsedBy.Name = "ColumnUsedBy";
            ColumnUsedBy.ReadOnly = true;
            //
            // ColumnFile
            //
            ColumnFile.FillWeight = 25F;
            ColumnFile.HeaderText = "File";
            ColumnFile.Name = "ColumnFile";
            ColumnFile.ReadOnly = true;
            //
            // ColumnSubject
            //
            ColumnSubject.FillWeight = 20F;
            ColumnSubject.HeaderText = "Subject";
            ColumnSubject.Name = "ColumnSubject";
            ColumnSubject.ReadOnly = true;
            //
            // buttonAdd
            //
            buttonAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonAdd.Location = new Point(12, 354);
            buttonAdd.Name = "buttonAdd";
            buttonAdd.Size = new Size(95, 28);
            buttonAdd.TabIndex = 1;
            buttonAdd.Text = "Add...";
            buttonAdd.UseVisualStyleBackColor = true;
            buttonAdd.Click += ButtonAdd_Click;
            //
            // buttonEdit
            //
            buttonEdit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonEdit.Location = new Point(113, 354);
            buttonEdit.Name = "buttonEdit";
            buttonEdit.Size = new Size(95, 28);
            buttonEdit.TabIndex = 2;
            buttonEdit.Text = "Edit...";
            buttonEdit.UseVisualStyleBackColor = true;
            buttonEdit.Click += ButtonEdit_Click;
            //
            // buttonDelete
            //
            buttonDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonDelete.Location = new Point(214, 354);
            buttonDelete.Name = "buttonDelete";
            buttonDelete.Size = new Size(95, 28);
            buttonDelete.TabIndex = 3;
            buttonDelete.Text = "Delete";
            buttonDelete.UseVisualStyleBackColor = true;
            buttonDelete.Click += ButtonDelete_Click;
            //
            // buttonRefresh
            //
            buttonRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonRefresh.Location = new Point(315, 354);
            buttonRefresh.Name = "buttonRefresh";
            buttonRefresh.Size = new Size(95, 28);
            buttonRefresh.TabIndex = 4;
            buttonRefresh.Text = "Refresh";
            buttonRefresh.UseVisualStyleBackColor = true;
            buttonRefresh.Click += ButtonRefresh_Click;
            //
            // buttonClose
            //
            buttonClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonClose.DialogResult = DialogResult.OK;
            buttonClose.Location = new Point(773, 382);
            buttonClose.Name = "buttonClose";
            buttonClose.Size = new Size(95, 28);
            buttonClose.TabIndex = 5;
            buttonClose.Text = "Close";
            buttonClose.UseVisualStyleBackColor = true;
            //
            // labelInfo
            //
            labelInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelInfo.AutoSize = true;
            labelInfo.Location = new Point(12, 392);
            labelInfo.Name = "labelInfo";
            labelInfo.Size = new Size(502, 15);
            labelInfo.TabIndex = 6;
            labelInfo.Text = "Proxies use a certificate by its name. A renewed file is picked up by every proxy that uses it.";
            //
            // FormCertificates
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonClose;
            ClientSize = new Size(880, 420);
            Controls.Add(labelInfo);
            Controls.Add(buttonClose);
            Controls.Add(buttonRefresh);
            Controls.Add(buttonDelete);
            Controls.Add(buttonEdit);
            Controls.Add(buttonAdd);
            Controls.Add(dataGridViewCertificates);
            MinimumSize = new Size(720, 360);
            Name = "FormCertificates";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Certificates";
            ((System.ComponentModel.ISupportInitialize)dataGridViewCertificates).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dataGridViewCertificates;
        private DataGridViewTextBoxColumn ColumnName;
        private DataGridViewTextBoxColumn ColumnExpires;
        private DataGridViewTextBoxColumn ColumnDaysLeft;
        private DataGridViewTextBoxColumn ColumnUsedBy;
        private DataGridViewTextBoxColumn ColumnFile;
        private DataGridViewTextBoxColumn ColumnSubject;
        private Button buttonAdd;
        private Button buttonEdit;
        private Button buttonDelete;
        private Button buttonRefresh;
        private Button buttonClose;
        private Label labelInfo;
    }
}
