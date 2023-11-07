using NetProxy.Client.Controls;

namespace NetProxy.Client.Forms
{
    partial class FormProgress
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormProgress));
            spinningProgress1 = new SpinningProgress();
            cmdCancel = new Button();
            pbProgress = new ProgressBar();
            lblHeader = new Label();
            lblBody = new Label();
            SuspendLayout();
            // 
            // spinningProgress1
            // 
            spinningProgress1.AutoIncrementFrequency = 100D;
            spinningProgress1.Location = new Point(14, 14);
            spinningProgress1.Margin = new Padding(5, 3, 5, 3);
            spinningProgress1.Name = "spinningProgress1";
            spinningProgress1.Size = new Size(35, 35);
            spinningProgress1.TabIndex = 0;
            // 
            // cmdCancel
            // 
            cmdCancel.Enabled = false;
            cmdCancel.Location = new Point(286, 120);
            cmdCancel.Margin = new Padding(4, 3, 4, 3);
            cmdCancel.Name = "cmdCancel";
            cmdCancel.Size = new Size(88, 27);
            cmdCancel.TabIndex = 1;
            cmdCancel.Text = "Cancel";
            cmdCancel.UseVisualStyleBackColor = true;
            cmdCancel.Click += cmdCancel_Click;
            // 
            // pbProgress
            // 
            pbProgress.Location = new Point(59, 87);
            pbProgress.Margin = new Padding(4, 3, 4, 3);
            pbProgress.Name = "pbProgress";
            pbProgress.Size = new Size(314, 27);
            pbProgress.Style = ProgressBarStyle.Marquee;
            pbProgress.TabIndex = 2;
            // 
            // lblHeader
            // 
            lblHeader.Location = new Point(56, 14);
            lblHeader.Margin = new Padding(4, 0, 4, 0);
            lblHeader.Name = "lblHeader";
            lblHeader.Size = new Size(317, 38);
            lblHeader.TabIndex = 3;
            lblHeader.Text = "Header Text";
            // 
            // lblBody
            // 
            lblBody.Location = new Point(56, 52);
            lblBody.Margin = new Padding(4, 0, 4, 0);
            lblBody.Name = "lblBody";
            lblBody.Size = new Size(317, 31);
            lblBody.TabIndex = 4;
            lblBody.Text = "Body Text";
            // 
            // FormProgress
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(387, 160);
            ControlBox = false;
            Controls.Add(lblBody);
            Controls.Add(lblHeader);
            Controls.Add(pbProgress);
            Controls.Add(cmdCancel);
            Controls.Add(spinningProgress1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormProgress";
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Please wait...";
            Shown += FormProgress_Shown;
            ResumeLayout(false);
        }

        #endregion

        private SpinningProgress spinningProgress1;
        private Button cmdCancel;
        private ProgressBar pbProgress;
        private Label lblHeader;
        private Label lblBody;
    }
}