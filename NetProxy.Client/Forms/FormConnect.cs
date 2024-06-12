using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Utilities;
using NTDLS.Persistence;
using NTDLS.ReliableMessaging;
using System.ComponentModel;

namespace NetProxy.Client.Forms
{
    public partial class FormConnect : Form
    {
        private readonly ConnectionInfo _connectionInfo = new();
        private FormProgress? _formProgress = null;
        private AutoResetEvent? _loginConnectionEvent = null;
        private BackgroundWorker? _worker = null;
        private string _connectMessage = string.Empty;
        private bool _loginResult = false;
        private RmClient? _messageClient = null;

        public ConnectionInfo GetConnectionInfo()
        {
            return _connectionInfo;
        }

        public FormConnect()
        {
            InitializeComponent();
        }

        private void FormConnect_Load(object? sender, EventArgs e)
        {
            AcceptButton = buttonConnect;

            var preferences = CommonApplicationData.LoadFromDisk(Constants.TitleCaption,
                new LoginFormPreferences
                {
                    ServerName = "127.0.0.1",
                    Username = "administrator"
                });

            textBoxServer.Text = preferences.ServerName;
            textBoxUsername.Text = preferences.Username;
        }

        private void buttonConnect_Click(object? sender, EventArgs e)
        {
            string verbatimServerName = textBoxServer.Text;
            string verbatimUsername = textBoxUsername.Text;

            _connectionInfo.ServerName = verbatimServerName.Trim();
            _connectionInfo.UserName = verbatimUsername.Trim();
            _connectionInfo.Password = textBoxPassword.Text.Trim();
            _connectionInfo.Port = Constants.DefaultManagementPort;

            int portBegin = _connectionInfo.ServerName.IndexOf(':');
            if (portBegin > 0)
            {
                try
                {
                    _connectionInfo.Port = int.Parse(_connectionInfo.ServerName.Substring(portBegin + 1));
                    _connectionInfo.ServerName = _connectionInfo.ServerName.Substring(0, portBegin);
                }
                catch
                {
                    MessageBox.Show("The port number could not be parsed. Expected format \"ServerName\" or \"ServerName:Port\".", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return;
                }
            }

            if (TestConnection())
            {
                CommonApplicationData.SaveToDisk(Constants.TitleCaption,
                    new LoginFormPreferences
                    {
                        ServerName = verbatimServerName,
                        Username = verbatimUsername
                    });

                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(_connectMessage, Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
        }

        private bool TestConnection()
        {
            _worker = new BackgroundWorker() { WorkerReportsProgress = true };
            _worker.DoWork += Worker_DoWork;
            _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            _worker.ProgressChanged += Worker_ProgressChanged;
            _worker.RunWorkerAsync();

            using (_formProgress = new FormProgress())
            {
                if (_formProgress.ShowDialog() == DialogResult.OK)
                {
                    return true;
                }
            }

            return false;
        }

        private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            _formProgress?.UpdateStatus(e.UserState as ProgressFormStatus);
        }

        private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            if ((e.Result as bool?) == true)
            {
                _formProgress?.Close(DialogResult.OK);
            }
            else
            {
                _formProgress?.Close(DialogResult.Cancel);
            }
        }

        private void Worker_DoWork(object? sender, DoWorkEventArgs e)
        {
            e.Result = false;
            _loginResult = false;
            _connectMessage = "Failed to connect.";

            _messageClient = new RmClient();

            NpUtility.EnsureNotNull(_worker);

            try
            {
                _worker.ReportProgress(0, new ProgressFormStatus() { Header = "Connecting..." });

                try
                {
                    _messageClient.Connect(_connectionInfo.ServerName, _connectionInfo.Port);
                    _worker.ReportProgress(0, new ProgressFormStatus() { Header = "Logging in..." });
                    _loginConnectionEvent = new AutoResetEvent(false);

                    _messageClient.Query<QueryLoginReply>(new QueryLogin(_connectionInfo.UserName, NpUtility.Sha256(_connectionInfo.Password))).ContinueWith((o) =>
                        {
                            if (o.IsCompletedSuccessfully && o.Result?.Result == true)
                            {
                                NpUtility.EnsureNotNull(_loginConnectionEvent);

                                _loginResult = o.Result.Result;
                                _loginConnectionEvent.Set();
                            }
                        });

                    if (_loginConnectionEvent.WaitOne(5000))
                    {
                        _worker.ReportProgress(0, new ProgressFormStatus() { Header = "Logging in..." });

                        e.Result = _loginResult;

                        if (_loginResult == false)
                        {
                            _connectMessage = "Unknown user or bad password.";
                        }
                    }
                }
                catch
                {
                    _connectMessage = "Could not connect to remote server.";
                }
            }
            catch (Exception ex)
            {
                _connectMessage = "An error occurred while logging in: " + ex.Message;
            }

            _messageClient.Disconnect();
        }

        private void buttonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void FormConnect_Shown(object? sender, EventArgs e)
        {
            if (textBoxServer.Text.Length > 0 && textBoxUsername.Text.Length > 0)
            {
                textBoxPassword.Focus();
            }
        }

        private void buttonAbout_Click(object sender, EventArgs e)
        {
            using var form = new FormAbout();
            form.ShowDialog();
        }
    }
}
