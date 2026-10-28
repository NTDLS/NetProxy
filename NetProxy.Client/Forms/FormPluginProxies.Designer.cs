namespace NetProxy.Client.Forms
{
    partial class FormPluginProxies
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormPluginProxies));
            labelPrompt = new Label();
            checkBoxCheckAll = new CheckBox();
            checkedListBoxProxies = new CheckedListBox();
            buttonOk = new Button();
            buttonCancel = new Button();
            SuspendLayout();
            //
            // labelPrompt
            //
            labelPrompt.Location = new Point(12, 12);
            labelPrompt.Name = "labelPrompt";
            labelPrompt.Size = new Size(320, 34);
            labelPrompt.TabIndex = 0;
            labelPrompt.Text = "Choose the proxies that use the plugin.";
            //
            // checkBoxCheckAll
            //
            checkBoxCheckAll.AutoSize = true;
            checkBoxCheckAll.Location = new Point(14, 49);
            checkBoxCheckAll.Name = "checkBoxCheckAll";
            checkBoxCheckAll.Size = new Size(77, 19);
            checkBoxCheckAll.TabIndex = 1;
            checkBoxCheckAll.Text = "Check all";
            checkBoxCheckAll.UseVisualStyleBackColor = true;
            checkBoxCheckAll.CheckedChanged += CheckBoxCheckAll_CheckedChanged;
            //
            // checkedListBoxProxies
            //
            checkedListBoxProxies.CheckOnClick = true;
            checkedListBoxProxies.FormattingEnabled = true;
            checkedListBoxProxies.IntegralHeight = false;
            checkedListBoxProxies.Location = new Point(12, 74);
            checkedListBoxProxies.Name = "checkedListBoxProxies";
            checkedListBoxProxies.Size = new Size(320, 275);
            checkedListBoxProxies.TabIndex = 2;
            checkedListBoxProxies.ItemCheck += CheckedListBoxProxies_ItemCheck;
            //
            // buttonOk
            //
            buttonOk.DialogResult = DialogResult.OK;
            buttonOk.Location = new Point(150, 355);
            buttonOk.Name = "buttonOk";
            buttonOk.Size = new Size(88, 27);
            buttonOk.TabIndex = 3;
            buttonOk.Text = "Ok";
            buttonOk.UseVisualStyleBackColor = true;
            //
            // buttonCancel
            //
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(244, 355);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(88, 27);
            buttonCancel.TabIndex = 4;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            //
            // FormPluginProxies
            //
            AcceptButton = buttonOk;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(344, 392);
            Controls.Add(labelPrompt);
            Controls.Add(checkBoxCheckAll);
            Controls.Add(checkedListBoxProxies);
            Controls.Add(buttonOk);
            Controls.Add(buttonCancel);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormPluginProxies";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Select proxies";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelPrompt;
        private CheckBox checkBoxCheckAll;
        private CheckedListBox checkedListBoxProxies;
        private Button buttonOk;
        private Button buttonCancel;
    }
}
