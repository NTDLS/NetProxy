using NetProxy.Client.Classes;
using NetProxy.Client.Properties;
using NetProxy.Library;
using NTDLS.Helpers;
using NTDLS.Localization;
using NTDLS.Persistence;
using NTDLS.WinFormsHelpers;
using NTDLS.WinFormsHelpers.Forms;
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
            FormLocalizer.Apply(this);
            Theming.InitializeForm(this);
            SetupToolTips();
        }

        /// <summary>
        /// Raised after the user has chosen a language and it has been applied and saved.
        /// </summary>
        public event EventHandler? LanguageChanged;

        private ToolTip? _toolTips;

        private void ButtonLanguage_Click(object? sender, EventArgs e)
        {
            var settings = UISettings.Load();

            using var form = new FormLanguage(settings.Language) { Icon = Icon };
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            settings.Language = form.SelectedLanguage;
            Localizer.Default.SetLanguage(settings.Language);
            settings.Save();

            FormLocalizer.Apply(this);
            SetupToolTips();
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the tool-tips in the current language. Called at the start and whenever the language changes.
        /// </summary>
        private void SetupToolTips()
        {
            var toolTips = _toolTips ??= ToolTipHelpers.CreateToolTipControl(this);

            toolTips.AddControls([label1, textBoxServer],
                Localize("The machine that the NetProxy service runs on: a name, an IPv4 address or an IPv6 address."
                + " Add a port after a name or IPv4 address (name:5854), or after an IPv6 address in brackets ([::1]:5854). The default port is 5854."));

            toolTips.AddControls([label2, textBoxUsername],
                Localize("The name of a user of the service."));

            toolTips.AddControls([label3, textBoxPassword],
                Localize("The password of the user."));
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
                    throw new Exception(Localize("The server address is missing its closing bracket. Expected format \"[IPv6 address]\" or \"[IPv6 address]:Port\"."));
                }

                serverName = text[1..end];

                var rest = text[(end + 1)..];
                if (rest.Length > 0)
                {
                    if (rest[0] != ':' || int.TryParse(rest[1..], out port) == false)
                    {
                        throw new Exception(Localize("The port number could not be parsed. Expected format \"[IPv6 address]:Port\"."));
                    }
                }
            }
            else if (text.Count(c => c == ':') == 1)
            {
                var colon = text.IndexOf(':');
                serverName = text[..colon];

                if (int.TryParse(text[(colon + 1)..], out port) == false)
                {
                    throw new Exception(Localize("The port number could not be parsed. Expected format \"ServerName\" or \"ServerName:Port\"."));
                }
            }
            else
            {
                serverName = text; //A name, an IPv4 address, or an IPv6 address.
            }

            if (port < 1 || port > 65535)
            {
                throw new Exception(Localize("The port number must be between 1 and 65535."));
            }
        }

        private void ButtonConnect_Click(object? sender, EventArgs e)
        {
            string verbatimServerName;
            string verbatimUsername;

            try
            {
                verbatimServerName = textBoxServer.GetAndValidateText(Localize("The server name is required."));
                verbatimUsername = textBoxUsername.GetAndValidateText(Localize("The user name is required."));

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
            _formProgress.SetHeaderText(e.UserState as string ?? Localize("Please wait..."));
        }

        private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            _formProgress.Close((e.Result as bool?) == true ? DialogResult.OK : DialogResult.Cancel);
        }

        private void Worker_DoWork(object? sender, DoWorkEventArgs e)
        {
            e.Result = false;

            _worker.EnsureNotNull().ReportProgress(0, Localize("Connecting..."));

            //Connects and logs in, which is what every other window of the console does too.
            var client = MessageClientFactory.Create(_connectionInfo, out var error);

            if (client == null)
            {
                _connectMessage = error ?? Localize("Failed to connect.");
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
            using var form = new FormAbout(Constants.FriendlyName, this.Icon, Resources.Logo128);
            form.ShowDialog();
        }
    }
}
