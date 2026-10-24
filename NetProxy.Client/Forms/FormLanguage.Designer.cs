namespace NetProxy.Client.Forms
{
    partial class FormLanguage
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormLanguage));
            labelPrompt = new Label();
            listBoxLanguages = new ListBox();
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
            labelPrompt.Text = "Choose the language of the user interface.";
            // 
            // listBoxLanguages
            // 
            listBoxLanguages.FormattingEnabled = true;
            listBoxLanguages.IntegralHeight = false;
            listBoxLanguages.Location = new Point(12, 49);
            listBoxLanguages.Name = "listBoxLanguages";
            listBoxLanguages.Size = new Size(320, 300);
            listBoxLanguages.TabIndex = 1;
            listBoxLanguages.DoubleClick += ListBoxLanguages_DoubleClick;
            // 
            // buttonOk
            // 
            buttonOk.DialogResult = DialogResult.OK;
            buttonOk.Location = new Point(150, 355);
            buttonOk.Name = "buttonOk";
            buttonOk.Size = new Size(88, 27);
            buttonOk.TabIndex = 2;
            buttonOk.Text = "Ok";
            buttonOk.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(244, 355);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(88, 27);
            buttonCancel.TabIndex = 3;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            // 
            // FormLanguage
            // 
            AcceptButton = buttonOk;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(344, 392);
            Controls.Add(labelPrompt);
            Controls.Add(listBoxLanguages);
            Controls.Add(buttonOk);
            Controls.Add(buttonCancel);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormLanguage";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Language";
            ResumeLayout(false);
        }

        #endregion

        private Label labelPrompt;
        private ListBox listBoxLanguages;
        private Button buttonOk;
        private Button buttonCancel;
    }
}
