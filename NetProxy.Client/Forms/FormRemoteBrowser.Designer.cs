namespace NetProxy.Client.Forms
{
    partial class FormRemoteBrowser
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
            buttonUp = new Button();
            textBoxPath = new TextBox();
            dataGridViewEntries = new DataGridView();
            ColumnName = new DataGridViewTextBoxColumn();
            ColumnType = new DataGridViewTextBoxColumn();
            ColumnSize = new DataGridViewTextBoxColumn();
            ColumnModified = new DataGridViewTextBoxColumn();
            checkBoxAllFiles = new CheckBox();
            labelStatus = new Label();
            buttonSelect = new Button();
            buttonCancel = new Button();
            ((System.ComponentModel.ISupportInitialize)dataGridViewEntries).BeginInit();
            SuspendLayout();
            //
            // dataGridViewCellStyle1
            //
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleRight;
            //
            // buttonUp
            //
            buttonUp.Location = new Point(12, 12);
            buttonUp.Name = "buttonUp";
            buttonUp.Size = new Size(60, 25);
            buttonUp.TabIndex = 0;
            buttonUp.Text = "Up";
            buttonUp.UseVisualStyleBackColor = true;
            buttonUp.Click += ButtonUp_Click;
            //
            // textBoxPath
            //
            textBoxPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxPath.Location = new Point(78, 13);
            textBoxPath.Name = "textBoxPath";
            textBoxPath.Size = new Size(550, 23);
            textBoxPath.TabIndex = 1;
            textBoxPath.KeyDown += TextBoxPath_KeyDown;
            //
            // dataGridViewEntries
            //
            dataGridViewEntries.AllowUserToAddRows = false;
            dataGridViewEntries.AllowUserToDeleteRows = false;
            dataGridViewEntries.AllowUserToResizeRows = false;
            dataGridViewEntries.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewEntries.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewEntries.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewEntries.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewEntries.Columns.AddRange(new DataGridViewColumn[] { ColumnName, ColumnType, ColumnSize, ColumnModified });
            dataGridViewEntries.Location = new Point(12, 46);
            dataGridViewEntries.MultiSelect = false;
            dataGridViewEntries.Name = "dataGridViewEntries";
            dataGridViewEntries.ReadOnly = true;
            dataGridViewEntries.RowHeadersVisible = false;
            dataGridViewEntries.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewEntries.Size = new Size(616, 296);
            dataGridViewEntries.TabIndex = 2;
            dataGridViewEntries.CellDoubleClick += DataGridViewEntries_CellDoubleClick;
            dataGridViewEntries.KeyDown += DataGridViewEntries_KeyDown;
            dataGridViewEntries.SelectionChanged += DataGridViewEntries_SelectionChanged;
            //
            // ColumnName
            //
            ColumnName.FillWeight = 52F;
            ColumnName.HeaderText = "Name";
            ColumnName.Name = "ColumnName";
            ColumnName.ReadOnly = true;
            //
            // ColumnType
            //
            ColumnType.FillWeight = 14F;
            ColumnType.HeaderText = "Type";
            ColumnType.Name = "ColumnType";
            ColumnType.ReadOnly = true;
            //
            // ColumnSize
            //
            ColumnSize.DefaultCellStyle = dataGridViewCellStyle1;
            ColumnSize.FillWeight = 14F;
            ColumnSize.HeaderText = "Size";
            ColumnSize.Name = "ColumnSize";
            ColumnSize.ReadOnly = true;
            //
            // ColumnModified
            //
            ColumnModified.FillWeight = 20F;
            ColumnModified.HeaderText = "Modified";
            ColumnModified.Name = "ColumnModified";
            ColumnModified.ReadOnly = true;
            //
            // checkBoxAllFiles
            //
            checkBoxAllFiles.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            checkBoxAllFiles.AutoSize = true;
            checkBoxAllFiles.Location = new Point(12, 350);
            checkBoxAllFiles.Name = "checkBoxAllFiles";
            checkBoxAllFiles.Size = new Size(105, 19);
            checkBoxAllFiles.TabIndex = 3;
            checkBoxAllFiles.Text = "Show all files";
            checkBoxAllFiles.UseVisualStyleBackColor = true;
            checkBoxAllFiles.CheckedChanged += CheckBoxAllFiles_CheckedChanged;
            //
            // labelStatus
            //
            labelStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelStatus.AutoEllipsis = true;
            labelStatus.Location = new Point(12, 376);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(616, 18);
            labelStatus.TabIndex = 4;
            //
            // buttonSelect
            //
            buttonSelect.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonSelect.Enabled = false;
            buttonSelect.Location = new Point(472, 402);
            buttonSelect.Name = "buttonSelect";
            buttonSelect.Size = new Size(75, 27);
            buttonSelect.TabIndex = 5;
            buttonSelect.Text = "Select";
            buttonSelect.UseVisualStyleBackColor = true;
            buttonSelect.Click += ButtonSelect_Click;
            //
            // buttonCancel
            //
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(553, 402);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 27);
            buttonCancel.TabIndex = 6;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            //
            // FormRemoteBrowser
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(640, 440);
            Controls.Add(buttonCancel);
            Controls.Add(buttonSelect);
            Controls.Add(labelStatus);
            Controls.Add(checkBoxAllFiles);
            Controls.Add(dataGridViewEntries);
            Controls.Add(textBoxPath);
            Controls.Add(buttonUp);
            MinimumSize = new Size(480, 320);
            Name = "FormRemoteBrowser";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Browse the service machine";
            Shown += FormRemoteBrowser_Shown;
            ((System.ComponentModel.ISupportInitialize)dataGridViewEntries).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button buttonUp;
        private TextBox textBoxPath;
        private DataGridView dataGridViewEntries;
        private DataGridViewTextBoxColumn ColumnName;
        private DataGridViewTextBoxColumn ColumnType;
        private DataGridViewTextBoxColumn ColumnSize;
        private DataGridViewTextBoxColumn ColumnModified;
        private CheckBox checkBoxAllFiles;
        private Label labelStatus;
        private Button buttonSelect;
        private Button buttonCancel;
    }
}
