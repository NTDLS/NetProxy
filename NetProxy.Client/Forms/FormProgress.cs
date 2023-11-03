namespace NetProxy.Client.Forms
{
    public partial class FormProgress : Form
    {
        public volatile bool IsLoaded = false;
        private System.Windows.Forms.Timer _timer = new();

        public void WaitForLoaded()
        {
            while (IsLoaded == false)
            {
                System.Threading.Thread.Sleep(10);
            }
        }

        #region Events

        public class OnCancelInfo
        {
            public bool Cancel = false;
        }

        public delegate void EventOnCancel(object? sender, OnCancelInfo e);
        public event EventOnCancel OnCancel;

        #endregion

        public FormProgress()
        {
            InitializeComponent();

            lblHeader.Text = "Please wait...";
            lblBody.Text = "";
            cmdCancel.Enabled = false;
            pbProgress.Minimum = 0;
            pbProgress.Maximum = 100;

            this.DialogResult = DialogResult.OK;
        }

        private void cmdCancel_Click(object? sender, EventArgs e)
        {
            if (OnCancel != null)
            {
                OnCancelInfo onCancelInfo = new OnCancelInfo();
                OnCancel(this, onCancelInfo);
                if (onCancelInfo.Cancel)
                {
                    return;
                }
            }

            this.DialogResult = DialogResult.Cancel;
            this.Close();

        }

        #region Properties.
        public bool CanCancel
        {
            get { return cmdCancel.Enabled; }
            set { cmdCancel.Enabled = value; }
        }
        public string CaptionText
        {
            get { return this.Text; }
            set { this.Text = value; }
        }
        public string HeaderText
        {
            get { return this.lblHeader.Text; }
            set { this.lblHeader.Text = value; }
        }
        public string BodyText
        {
            get { return this.lblBody.Text; }
            set { this.lblBody.Text = value; }
        }
        public int ProgressMinimum
        {
            get { return pbProgress.Minimum; }
            set { pbProgress.Minimum = value; }
        }
        public int ProgressMaximum
        {
            get { return pbProgress.Maximum; }
            set { pbProgress.Maximum = value; }
        }
        public int ProgressPosition
        {
            get { return pbProgress.Value; }
            set
            {
                if (pbProgress.Value > 0 && pbProgress.Style == ProgressBarStyle.Marquee)
                {
                    pbProgress.Style = ProgressBarStyle.Continuous;
                }
                pbProgress.Value = value;
            }
        }
        public ProgressBarStyle ProgressStyle
        {
            get { return pbProgress.Style; }
            set { pbProgress.Style = value; }
        }
        #endregion

        public DialogResult ShowDialog(int timeoutMs)
        {
            _timer = new();
            _timer.Tick += Timer_Tick;
            _timer.Interval = timeoutMs;
            _timer.Start();

            DialogResult result = this.ShowDialog();

            _timer.Stop();

            return result;
        }

        public void Close(DialogResult result)
        {
            this.DialogResult = result;
            this.Close();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        public void UpdateStatus(ProgressFormStatus status)
        {
            if (status != null)
            {
                if (status.Caption != null)
                {
                    this.CaptionText = status.Caption;
                }

                if (status.Header != null)
                {
                    this.HeaderText = status.Header;
                }

                if (status.Body != null)
                {
                    this.BodyText = status.Body;
                }

                if (status.ProgressValue != null)
                {
                    this.ProgressPosition = (int)status.ProgressValue;
                }

                if (status.ProgressMinimum != null)
                {
                    this.ProgressMinimum = (int)status.ProgressMinimum;
                }

                if (status.ProgressMaximum != null)
                {
                    this.ProgressMaximum = (int)status.ProgressMaximum;
                }
            }
        }

        private void FormProgress_Shown(object? sender, EventArgs e)
        {
            IsLoaded = true;
        }
    }
}
