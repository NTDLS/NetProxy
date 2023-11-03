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
        public event EventOnCancel? OnCancel;

        #endregion

        public FormProgress()
        {
            InitializeComponent();

            lblHeader.Text = "Please wait...";
            lblBody.Text = "";
            cmdCancel.Enabled = false;
            pbProgress.Minimum = 0;
            pbProgress.Maximum = 100;

            DialogResult = DialogResult.OK;
        }

        private void cmdCancel_Click(object? sender, EventArgs e)
        {
            if (OnCancel != null)
            {
                OnCancelInfo onCancelInfo = new();
                OnCancel(this, onCancelInfo);
                if (onCancelInfo.Cancel)
                {
                    return;
                }
            }

            DialogResult = DialogResult.Cancel;
            Close();

        }

        #region Properties.
        public bool CanCancel
        {
            get { return cmdCancel.Enabled; }
            set { cmdCancel.Enabled = value; }
        }
        public string CaptionText
        {
            get { return Text; }
            set { Text = value; }
        }
        public string HeaderText
        {
            get { return lblHeader.Text; }
            set { lblHeader.Text = value; }
        }
        public string BodyText
        {
            get { return lblBody.Text; }
            set { lblBody.Text = value; }
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

            DialogResult result = ShowDialog();

            _timer.Stop();

            return result;
        }

        public void Close(DialogResult result)
        {
            DialogResult = result;
            Close();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        public void UpdateStatus(ProgressFormStatus? status)
        {
            if (status != null)
            {
                if (status.Caption != null)
                {
                    CaptionText = status.Caption;
                }

                if (status.Header != null)
                {
                    HeaderText = status.Header;
                }

                if (status.Body != null)
                {
                    BodyText = status.Body;
                }

                if (status.ProgressValue != null)
                {
                    ProgressPosition = (int)status.ProgressValue;
                }

                if (status.ProgressMinimum != null)
                {
                    ProgressMinimum = (int)status.ProgressMinimum;
                }

                if (status.ProgressMaximum != null)
                {
                    ProgressMaximum = (int)status.ProgressMaximum;
                }
            }
        }

        private void FormProgress_Shown(object? sender, EventArgs e)
        {
            IsLoaded = true;
        }
    }
}
