namespace NetProxy.Client.Forms
{
    partial class FormPlugins
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
            dataGridViewPlugins = new DataGridView();
            ColumnOrder = new DataGridViewTextBoxColumn();
            ColumnName = new DataGridViewTextBoxColumn();
            ColumnEnabled = new DataGridViewTextBoxColumn();
            ColumnStatus = new DataGridViewTextBoxColumn();
            ColumnPlugin = new DataGridViewTextBoxColumn();
            ColumnVersion = new DataGridViewTextBoxColumn();
            ColumnAssembly = new DataGridViewTextBoxColumn();
            buttonAdd = new Button();
            buttonEdit = new Button();
            buttonDelete = new Button();
            buttonMoveUp = new Button();
            buttonMoveDown = new Button();
            buttonReload = new Button();
            buttonClose = new Button();
            labelOrderInfo = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlugins).BeginInit();
            SuspendLayout();
            //
            // dataGridViewPlugins
            //
            dataGridViewPlugins.AllowUserToAddRows = false;
            dataGridViewPlugins.AllowUserToDeleteRows = false;
            dataGridViewPlugins.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewPlugins.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewPlugins.BackgroundColor = SystemColors.ButtonFace;
            dataGridViewPlugins.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewPlugins.Columns.AddRange(new DataGridViewColumn[] { ColumnOrder, ColumnName, ColumnEnabled, ColumnStatus, ColumnPlugin, ColumnVersion, ColumnAssembly });
            dataGridViewPlugins.Location = new Point(12, 12);
            dataGridViewPlugins.MultiSelect = false;
            dataGridViewPlugins.Name = "dataGridViewPlugins";
            dataGridViewPlugins.ReadOnly = true;
            dataGridViewPlugins.RowHeadersVisible = false;
            dataGridViewPlugins.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewPlugins.Size = new Size(796, 330);
            dataGridViewPlugins.TabIndex = 0;
            dataGridViewPlugins.CellDoubleClick += DataGridViewPlugins_CellDoubleClick;
            //
            // ColumnOrder
            //
            ColumnOrder.FillWeight = 6F;
            ColumnOrder.HeaderText = "#";
            ColumnOrder.Name = "ColumnOrder";
            ColumnOrder.ReadOnly = true;
            //
            // ColumnName
            //
            ColumnName.FillWeight = 20F;
            ColumnName.HeaderText = "Name";
            ColumnName.Name = "ColumnName";
            ColumnName.ReadOnly = true;
            //
            // ColumnEnabled
            //
            ColumnEnabled.FillWeight = 10F;
            ColumnEnabled.HeaderText = "Enabled";
            ColumnEnabled.Name = "ColumnEnabled";
            ColumnEnabled.ReadOnly = true;
            //
            // ColumnStatus
            //
            ColumnStatus.FillWeight = 24F;
            ColumnStatus.HeaderText = "Status";
            ColumnStatus.Name = "ColumnStatus";
            ColumnStatus.ReadOnly = true;
            //
            // ColumnPlugin
            //
            ColumnPlugin.FillWeight = 16F;
            ColumnPlugin.HeaderText = "Plugin";
            ColumnPlugin.Name = "ColumnPlugin";
            ColumnPlugin.ReadOnly = true;
            //
            // ColumnVersion
            //
            ColumnVersion.FillWeight = 10F;
            ColumnVersion.HeaderText = "Version";
            ColumnVersion.Name = "ColumnVersion";
            ColumnVersion.ReadOnly = true;
            //
            // ColumnAssembly
            //
            ColumnAssembly.FillWeight = 30F;
            ColumnAssembly.HeaderText = "Assembly";
            ColumnAssembly.Name = "ColumnAssembly";
            ColumnAssembly.ReadOnly = true;
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
            // buttonMoveUp
            //
            buttonMoveUp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonMoveUp.Location = new Point(315, 354);
            buttonMoveUp.Name = "buttonMoveUp";
            buttonMoveUp.Size = new Size(95, 28);
            buttonMoveUp.TabIndex = 4;
            buttonMoveUp.Text = "Move up";
            buttonMoveUp.UseVisualStyleBackColor = true;
            buttonMoveUp.Click += ButtonMoveUp_Click;
            //
            // buttonMoveDown
            //
            buttonMoveDown.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonMoveDown.Location = new Point(416, 354);
            buttonMoveDown.Name = "buttonMoveDown";
            buttonMoveDown.Size = new Size(95, 28);
            buttonMoveDown.TabIndex = 5;
            buttonMoveDown.Text = "Move down";
            buttonMoveDown.UseVisualStyleBackColor = true;
            buttonMoveDown.Click += ButtonMoveDown_Click;
            //
            // buttonReload
            //
            buttonReload.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonReload.Location = new Point(517, 354);
            buttonReload.Name = "buttonReload";
            buttonReload.Size = new Size(95, 28);
            buttonReload.TabIndex = 6;
            buttonReload.Text = "Reload";
            buttonReload.UseVisualStyleBackColor = true;
            buttonReload.Click += ButtonReload_Click;
            //
            // buttonClose
            //
            buttonClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonClose.DialogResult = DialogResult.OK;
            buttonClose.Location = new Point(713, 382);
            buttonClose.Name = "buttonClose";
            buttonClose.Size = new Size(95, 28);
            buttonClose.TabIndex = 7;
            buttonClose.Text = "Close";
            buttonClose.UseVisualStyleBackColor = true;
            //
            // labelOrderInfo
            //
            labelOrderInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelOrderInfo.AutoSize = true;
            labelOrderInfo.Location = new Point(12, 392);
            labelOrderInfo.Name = "labelOrderInfo";
            labelOrderInfo.Size = new Size(466, 15);
            labelOrderInfo.TabIndex = 8;
            labelOrderInfo.Text = "Plugins are called in this order. A proxy uses the plugins that are ticked on its Plugins tab.";
            //
            // FormPlugins
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonClose;
            ClientSize = new Size(820, 420);
            Controls.Add(labelOrderInfo);
            Controls.Add(buttonClose);
            Controls.Add(buttonReload);
            Controls.Add(buttonMoveDown);
            Controls.Add(buttonMoveUp);
            Controls.Add(buttonDelete);
            Controls.Add(buttonEdit);
            Controls.Add(buttonAdd);
            Controls.Add(dataGridViewPlugins);
            MinimumSize = new Size(720, 360);
            Name = "FormPlugins";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Plugins";
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlugins).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dataGridViewPlugins;
        private DataGridViewTextBoxColumn ColumnOrder;
        private DataGridViewTextBoxColumn ColumnName;
        private DataGridViewTextBoxColumn ColumnEnabled;
        private DataGridViewTextBoxColumn ColumnStatus;
        private DataGridViewTextBoxColumn ColumnPlugin;
        private DataGridViewTextBoxColumn ColumnVersion;
        private DataGridViewTextBoxColumn ColumnAssembly;
        private Button buttonAdd;
        private Button buttonEdit;
        private Button buttonDelete;
        private Button buttonMoveUp;
        private Button buttonMoveDown;
        private Button buttonReload;
        private Button buttonClose;
        private Label labelOrderInfo;
    }
}
