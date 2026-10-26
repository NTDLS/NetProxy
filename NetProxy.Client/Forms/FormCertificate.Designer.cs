namespace NetProxy.Client.Forms
{
    partial class FormCertificate
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
            labelName = new Label();
            textBoxName = new TextBox();
            labelDescription = new Label();
            textBoxDescription = new TextBox();
            labelPath = new Label();
            textBoxPath = new TextBox();
            buttonBrowse = new Button();
            labelPassword = new Label();
            textBoxPassword = new TextBox();
            labelNotes = new Label();
            buttonSave = new Button();
            buttonCancel = new Button();
            SuspendLayout();
            //
            // labelName
            //
            labelName.AutoSize = true;
            labelName.Location = new Point(12, 15);
            labelName.Name = "labelName";
            labelName.Size = new Size(39, 15);
            labelName.TabIndex = 0;
            labelName.Text = "Name";
            //
            // textBoxName
            //
            textBoxName.Location = new Point(130, 12);
            textBoxName.Name = "textBoxName";
            textBoxName.Size = new Size(418, 23);
            textBoxName.TabIndex = 1;
            //
            // labelDescription
            //
            labelDescription.AutoSize = true;
            labelDescription.Location = new Point(12, 46);
            labelDescription.Name = "labelDescription";
            labelDescription.Size = new Size(67, 15);
            labelDescription.TabIndex = 2;
            labelDescription.Text = "Description";
            //
            // textBoxDescription
            //
            textBoxDescription.Location = new Point(130, 43);
            textBoxDescription.Name = "textBoxDescription";
            textBoxDescription.Size = new Size(418, 23);
            textBoxDescription.TabIndex = 3;
            //
            // labelPath
            //
            labelPath.AutoSize = true;
            labelPath.Location = new Point(12, 77);
            labelPath.Name = "labelPath";
            labelPath.Size = new Size(91, 15);
            labelPath.TabIndex = 4;
            labelPath.Text = "Certificate (PFX)";
            //
            // textBoxPath
            //
            textBoxPath.Location = new Point(130, 74);
            textBoxPath.Name = "textBoxPath";
            textBoxPath.Size = new Size(330, 23);
            textBoxPath.TabIndex = 5;
            //
            // buttonBrowse
            //
            buttonBrowse.Location = new Point(468, 73);
            buttonBrowse.Name = "buttonBrowse";
            buttonBrowse.Size = new Size(80, 25);
            buttonBrowse.TabIndex = 6;
            buttonBrowse.Text = "Browse...";
            buttonBrowse.UseVisualStyleBackColor = true;
            buttonBrowse.Click += ButtonBrowse_Click;
            //
            // labelPassword
            //
            labelPassword.AutoSize = true;
            labelPassword.Location = new Point(12, 108);
            labelPassword.Name = "labelPassword";
            labelPassword.Size = new Size(57, 15);
            labelPassword.TabIndex = 7;
            labelPassword.Text = "Password";
            //
            // textBoxPassword
            //
            textBoxPassword.Location = new Point(130, 105);
            textBoxPassword.Name = "textBoxPassword";
            textBoxPassword.Size = new Size(250, 23);
            textBoxPassword.TabIndex = 8;
            textBoxPassword.UseSystemPasswordChar = true;
            //
            // labelNotes
            //
            labelNotes.Location = new Point(12, 142);
            labelNotes.Name = "labelNotes";
            labelNotes.Size = new Size(536, 60);
            labelNotes.TabIndex = 9;
            labelNotes.Text = "The file is on the machine that the service runs on, and is watched: when it is replaced (a renewal) every proxy that uses the certificate uses the new one. Leave the password empty if the file has none. The service stores it encrypted.";
            //
            // buttonSave
            //
            buttonSave.Location = new Point(392, 214);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(75, 27);
            buttonSave.TabIndex = 10;
            buttonSave.Text = "Save";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            //
            // buttonCancel
            //
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(473, 214);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 27);
            buttonCancel.TabIndex = 11;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            //
            // FormCertificate
            //
            AcceptButton = buttonSave;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(560, 254);
            Controls.Add(buttonCancel);
            Controls.Add(buttonSave);
            Controls.Add(labelNotes);
            Controls.Add(textBoxPassword);
            Controls.Add(labelPassword);
            Controls.Add(buttonBrowse);
            Controls.Add(textBoxPath);
            Controls.Add(labelPath);
            Controls.Add(textBoxDescription);
            Controls.Add(labelDescription);
            Controls.Add(textBoxName);
            Controls.Add(labelName);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormCertificate";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Certificate";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelName;
        private TextBox textBoxName;
        private Label labelDescription;
        private TextBox textBoxDescription;
        private Label labelPath;
        private TextBox textBoxPath;
        private Button buttonBrowse;
        private Label labelPassword;
        private TextBox textBoxPassword;
        private Label labelNotes;
        private Button buttonSave;
        private Button buttonCancel;
    }
}
