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
            this.spinningProgress1 = new NetProxy.Client.Controls.SpinningProgress();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.pbProgress = new System.Windows.Forms.ProgressBar();
            this.lblHeader = new System.Windows.Forms.Label();
            this.lblBody = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // spinningProgress1
            // 
            this.spinningProgress1.AutoIncrementFrequency = 100D;
            this.spinningProgress1.Location = new System.Drawing.Point(12, 12);
            this.spinningProgress1.Name = "spinningProgress1";
            this.spinningProgress1.Size = new System.Drawing.Size(30, 30);
            this.spinningProgress1.TabIndex = 0;
            // 
            // cmdCancel
            // 
            this.cmdCancel.Enabled = false;
            this.cmdCancel.Location = new System.Drawing.Point(245, 104);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(75, 23);
            this.cmdCancel.TabIndex = 1;
            this.cmdCancel.Text = "Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // pbProgress
            // 
            this.pbProgress.Location = new System.Drawing.Point(51, 75);
            this.pbProgress.Name = "pbProgress";
            this.pbProgress.Size = new System.Drawing.Size(269, 23);
            this.pbProgress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.pbProgress.TabIndex = 2;
            // 
            // lblHeader
            // 
            this.lblHeader.Location = new System.Drawing.Point(48, 12);
            this.lblHeader.Name = "lblHeader";
            this.lblHeader.Size = new System.Drawing.Size(272, 33);
            this.lblHeader.TabIndex = 3;
            this.lblHeader.Text = "Header Text";
            // 
            // lblBody
            // 
            this.lblBody.Location = new System.Drawing.Point(48, 45);
            this.lblBody.Name = "lblBody";
            this.lblBody.Size = new System.Drawing.Size(272, 27);
            this.lblBody.TabIndex = 4;
            this.lblBody.Text = "Body Text";
            // 
            // FormProgress
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(332, 139);
            this.ControlBox = false;
            this.Controls.Add(this.lblBody);
            this.Controls.Add(this.lblHeader);
            this.Controls.Add(this.pbProgress);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.spinningProgress1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormProgress";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Please wait...";
            this.Shown += new System.EventHandler(this.FormProgress_Shown);
            this.ResumeLayout(false);

        }

        #endregion

        private SpinningProgress spinningProgress1;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.ProgressBar pbProgress;
        private System.Windows.Forms.Label lblHeader;
        private System.Windows.Forms.Label lblBody;
    }
}