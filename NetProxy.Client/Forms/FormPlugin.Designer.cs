namespace NetProxy.Client.Forms
{
    partial class FormPlugin
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormPlugin));
            labelName = new Label();
            textBoxName = new TextBox();
            labelDescription = new Label();
            textBoxDescription = new TextBox();
            checkBoxEnabled = new CheckBox();
            labelAssemblyPath = new Label();
            textBoxAssemblyPath = new TextBox();
            buttonBrowse = new Button();
            labelTypeName = new Label();
            textBoxTypeName = new TextBox();
            labelTimeout = new Label();
            textBoxTimeout = new TextBox();
            labelSettings = new Label();
            textBoxSettings = new TextBox();
            labelNotes = new Label();
            buttonSave = new Button();
            buttonCancel = new Button();
            SuspendLayout();
            // 
            // labelName
            // 
            labelName.AutoSize = true;
            labelName.Location = new Point(12, 9);
            labelName.Name = "labelName";
            labelName.Size = new Size(39, 15);
            labelName.TabIndex = 0;
            labelName.Text = "Name";
            // 
            // textBoxName
            // 
            textBoxName.Location = new Point(13, 27);
            textBoxName.Name = "textBoxName";
            textBoxName.Size = new Size(418, 23);
            textBoxName.TabIndex = 1;
            // 
            // labelDescription
            // 
            labelDescription.AutoSize = true;
            labelDescription.Location = new Point(12, 55);
            labelDescription.Name = "labelDescription";
            labelDescription.Size = new Size(67, 15);
            labelDescription.TabIndex = 2;
            labelDescription.Text = "Description";
            // 
            // textBoxDescription
            // 
            textBoxDescription.Location = new Point(13, 73);
            textBoxDescription.Name = "textBoxDescription";
            textBoxDescription.Size = new Size(418, 23);
            textBoxDescription.TabIndex = 3;
            // 
            // checkBoxEnabled
            // 
            checkBoxEnabled.AutoSize = true;
            checkBoxEnabled.Location = new Point(13, 393);
            checkBoxEnabled.Name = "checkBoxEnabled";
            checkBoxEnabled.Size = new Size(68, 19);
            checkBoxEnabled.TabIndex = 4;
            checkBoxEnabled.Text = "Enabled";
            checkBoxEnabled.UseVisualStyleBackColor = true;
            // 
            // labelAssemblyPath
            // 
            labelAssemblyPath.AutoSize = true;
            labelAssemblyPath.Location = new Point(12, 101);
            labelAssemblyPath.Name = "labelAssemblyPath";
            labelAssemblyPath.Size = new Size(85, 15);
            labelAssemblyPath.TabIndex = 5;
            labelAssemblyPath.Text = "Assembly (.dll)";
            // 
            // textBoxAssemblyPath
            // 
            textBoxAssemblyPath.Location = new Point(13, 119);
            textBoxAssemblyPath.Name = "textBoxAssemblyPath";
            textBoxAssemblyPath.Size = new Size(375, 23);
            textBoxAssemblyPath.TabIndex = 6;
            // 
            // buttonBrowse
            // 
            buttonBrowse.Location = new Point(394, 119);
            buttonBrowse.Name = "buttonBrowse";
            buttonBrowse.Size = new Size(37, 25);
            buttonBrowse.TabIndex = 7;
            buttonBrowse.Text = "...";
            buttonBrowse.UseVisualStyleBackColor = true;
            buttonBrowse.Click += ButtonBrowse_Click;
            // 
            // labelTypeName
            // 
            labelTypeName.AutoSize = true;
            labelTypeName.Location = new Point(12, 147);
            labelTypeName.Name = "labelTypeName";
            labelTypeName.Size = new Size(89, 15);
            labelTypeName.TabIndex = 8;
            labelTypeName.Text = "Class (optional)";
            // 
            // textBoxTypeName
            // 
            textBoxTypeName.Location = new Point(13, 165);
            textBoxTypeName.Name = "textBoxTypeName";
            textBoxTypeName.Size = new Size(418, 23);
            textBoxTypeName.TabIndex = 9;
            // 
            // labelTimeout
            // 
            labelTimeout.AutoSize = true;
            labelTimeout.Location = new Point(12, 193);
            labelTimeout.Name = "labelTimeout";
            labelTimeout.Size = new Size(88, 15);
            labelTimeout.TabIndex = 10;
            labelTimeout.Text = "Time limit (ms)";
            // 
            // textBoxTimeout
            // 
            textBoxTimeout.Location = new Point(13, 211);
            textBoxTimeout.Name = "textBoxTimeout";
            textBoxTimeout.Size = new Size(100, 23);
            textBoxTimeout.TabIndex = 11;
            // 
            // labelSettings
            // 
            labelSettings.AutoSize = true;
            labelSettings.Location = new Point(12, 239);
            labelSettings.Name = "labelSettings";
            labelSettings.Size = new Size(191, 15);
            labelSettings.TabIndex = 12;
            labelSettings.Text = "Settings (one name=value per line)";
            // 
            // textBoxSettings
            // 
            textBoxSettings.AcceptsReturn = true;
            textBoxSettings.Location = new Point(13, 257);
            textBoxSettings.Multiline = true;
            textBoxSettings.Name = "textBoxSettings";
            textBoxSettings.ScrollBars = ScrollBars.Vertical;
            textBoxSettings.Size = new Size(536, 130);
            textBoxSettings.TabIndex = 13;
            // 
            // labelNotes
            // 
            labelNotes.Location = new Point(3, 428);
            labelNotes.Name = "labelNotes";
            labelNotes.Size = new Size(536, 45);
            labelNotes.TabIndex = 14;
            labelNotes.Text = "The assembly path is on the machine that the service runs on. A relative path is relative to the folder of the service. The plugin reads its settings as typed here, so do not keep secrets in them.";
            // 
            // buttonSave
            // 
            buttonSave.Location = new Point(331, 476);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(100, 27);
            buttonSave.TabIndex = 15;
            buttonSave.Text = "Save";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(448, 476);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(100, 27);
            buttonCancel.TabIndex = 16;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            // 
            // FormPlugin
            // 
            AcceptButton = buttonSave;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(560, 513);
            Controls.Add(buttonCancel);
            Controls.Add(buttonSave);
            Controls.Add(labelNotes);
            Controls.Add(textBoxSettings);
            Controls.Add(labelSettings);
            Controls.Add(textBoxTimeout);
            Controls.Add(labelTimeout);
            Controls.Add(textBoxTypeName);
            Controls.Add(labelTypeName);
            Controls.Add(buttonBrowse);
            Controls.Add(textBoxAssemblyPath);
            Controls.Add(labelAssemblyPath);
            Controls.Add(checkBoxEnabled);
            Controls.Add(textBoxDescription);
            Controls.Add(labelDescription);
            Controls.Add(textBoxName);
            Controls.Add(labelName);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormPlugin";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Plugin";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelName;
        private TextBox textBoxName;
        private Label labelDescription;
        private TextBox textBoxDescription;
        private CheckBox checkBoxEnabled;
        private Label labelAssemblyPath;
        private TextBox textBoxAssemblyPath;
        private Button buttonBrowse;
        private Label labelTypeName;
        private TextBox textBoxTypeName;
        private Label labelTimeout;
        private TextBox textBoxTimeout;
        private Label labelSettings;
        private TextBox textBoxSettings;
        private Label labelNotes;
        private Button buttonSave;
        private Button buttonCancel;
    }
}
