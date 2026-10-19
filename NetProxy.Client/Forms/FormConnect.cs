using NetProxy.Client.Classes;
using NetProxy.Library;
using NTDLS.Helpers;
using NTDLS.Persistence;
using NTDLS.WinFormsHelpers;
using System.ComponentModel;

namespace NetProxy.Client.Forms
{
    public partial class FormConnect : Form
    {
        private readonly ConnectionInfo _connectionInfo = new();
        private readonly ProgressForm _formProgress = new();
        private BackgroundWorker? _worker = null;
        private string _connectMessage = string.Empty;

        public ConnectionInfo GetConnectionInfo()
        {
            return _connectionInfo;
        }

        public FormConnect()
        {
            InitializeComponent();
            Theming.InitializeForm(this);
        }

        private void FormConnect_Load(object? sender, EventArgs e)
        {
            AcceptButton = buttonConnect;

            var preferences = CommonApplicationData.LoadFromDisk(Constants.FriendlyName,
                new LoginFormPreferences
                {
                    ServerName = "127.0.0.1",
                    Username = "administrator"
                });

            textBoxServer.Text = preferences.ServerName;
            textBoxUsername.Text = preferences.Username;
        }

        /// <summary>
        /// Reads "name", "name:port", an IPv4 address (with or without a port), or an IPv6 address ("::1", or "[::1]:5854" with a port).
        /// An IPv6 address has colons of its own, so a port can only be given after it in square brackets.
        /// </summary>
        public static void ParseServer(string text, out string serverName, out int port)
        {
            text = text.Trim();
            port = Constants.DefaultManagementPort;

            if (text.StartsWith('['))
            {
                var end = text.IndexOf(']');
                if (end < 0)
                {
                    throw new Exception("The server address is missing its closing bracket. Expected format \"[IPv6 address]\" or \"[IPv6 address]:Port\".");
                }

                serverName = text[1..end];

                var rest = text[(end + 1)..];
                if (rest.Length > 0)
                {
                    if (rest[0] != ':' || int.TryParse(rest[1..], out port) == false)
                    {
                        throw new Exception("The port number could not be parsed. Expected format \"[IPv6 address]:Port\".");
                    }
                }
            }
            else if (text.Count(c => c == ':') == 1)
            {
                var colon = text.IndexOf(':');
                serverName = text[..colon];

                if (int.TryParse(text[(colon + 1)..], out port) == false)
                {
                    throw new Exception("The port number could not be parsed. Expected format \"ServerName\" or \"ServerName:Port\".");
                }
            }
            else
            {
                serverName = text; //A name, an IPv4 address, or an IPv6 address.
            }

            if (port < 1 || port > 65535)
            {
                throw new Exception("The port number must be between 1 and 65535.");
            }
        }

        private void ButtonConnect_Click(object? sender, EventArgs e)
        {
            string verbatimServerName;
            string verbatimUsername;

            try
            {
                verbatimServerName = textBoxServer.GetAndValidateText("The server name is required.");
                verbatimUsername = textBoxUsername.GetAndValidateText("The user name is required.");

                ParseServer(verbatimServerName, out var serverName, out var port);

                _connectionInfo.ServerName = serverName;
                _connectionInfo.Port = port;
                _connectionInfo.UserName = verbatimUsername.Trim();
                _connectionInfo.Password = textBoxPassword.Text; //Not trimmed: spaces are part of a password.
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (TestConnection())
            {
                CommonApplicationData.SaveToDisk(Constants.FriendlyName,
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
                MessageBox.Show(_connectMessage, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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

            return _formProgress.ShowDialog() == DialogResult.OK;
        }

        private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            _formProgress.SetHeaderText(e.UserState as string ?? "Please wait...");
        }

        private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            _formProgress.Close((e.Result as bool?) == true ? DialogResult.OK : DialogResult.Cancel);
        }

        private void Worker_DoWork(object? sender, DoWorkEventArgs e)
        {
            e.Result = false;

            _worker.EnsureNotNull().ReportProgress(0, "Connecting...");

            //Connects and logs in, which is what every other window of the console does too.
            var client = MessageClientFactory.Create(_connectionInfo, out var error);

            if (client == null)
            {
                _connectMessage = error ?? "Failed to connect.";
                return;
            }

            try
            {
                client.Disconnect();
            }
            catch
            {
            }

            e.Result = true;
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
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

        private void ButtonAbout_Click(object sender, EventArgs e)
        {
            using var form = new FormAbout();
            form.ShowDialog();
        }
    }
}
