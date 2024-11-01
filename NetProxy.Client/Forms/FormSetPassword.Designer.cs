namespace NetProxy.Client.Forms
{
    partial class FormSetPassword
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormSetPassword));
            textBoxPassword1 = new TextBox();
            label3 = new Label();
            buttonCancel = new Button();
            textBoxPassword2 = new TextBox();
            label1 = new Label();
            buttonSave = new Button();
            SuspendLayout();
            // 
            // textBoxPassword1
            // 
            textBoxPassword1.Location = new Point(86, 14);
            textBoxPassword1.Margin = new Padding(4, 3, 4, 3);
            textBoxPassword1.Name = "textBoxPassword1";
            textBoxPassword1.PasswordChar = '*';
            textBoxPassword1.Size = new Size(187, 23);
            textBoxPassword1.TabIndex = 0;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(18, 17);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(57, 15);
            label3.TabIndex = 9;
            label3.Text = "Password";
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(102, 74);
            buttonCancel.Margin = new Padding(4, 3, 4, 3);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(83, 27);
            buttonCancel.TabIndex = 3;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // textBoxPassword2
            // 
            textBoxPassword2.Location = new Point(86, 44);
            textBoxPassword2.Margin = new Padding(4, 3, 4, 3);
            textBoxPassword2.Name = "textBoxPassword2";
            textBoxPassword2.PasswordChar = '*';
            textBoxPassword2.Size = new Size(187, 23);
            textBoxPassword2.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(30, 47);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(51, 15);
            label1.TabIndex = 11;
            label1.Text = "Confirm";
            // 
            // buttonSave
            // 
            buttonSave.Location = new Point(191, 74);
            buttonSave.Margin = new Padding(4, 3, 4, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(83, 27);
            buttonSave.TabIndex = 2;
            buttonSave.Text = "Ok";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // FormSetPassword
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(298, 115);
            Controls.Add(buttonSave);
            Controls.Add(textBoxPassword2);
            Controls.Add(label1);
            Controls.Add(textBoxPassword1);
            Controls.Add(label3);
            Controls.Add(buttonCancel);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormSetPassword";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "FormSetPassword";
            Load += FormSetPassword_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox textBoxPassword1;
        private Label label3;
        private Button buttonCancel;
        private TextBox textBoxPassword2;
        private Label label1;
        private Button buttonSave;
    }
}