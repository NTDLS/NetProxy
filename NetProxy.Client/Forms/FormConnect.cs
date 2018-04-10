using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using NetProxy.Client.Classes;
using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Win32;
using Newtonsoft.Json;

namespace NetProxy.Client.Forms
{
    public partial class FormConnect : Form
    {
        private ConnectionInfo connectionInfo = new ConnectionInfo();
        private FormProgress formProgress = null;
        private AutoResetEvent loginConnectionEvent = null;
        private BackgroundWorker worker = null;
        private string connectMessage = string.Empty;
        private bool loginResult = false;
        private Packeteer packeteer = null;

        public ConnectionInfo GetConnectionInfo()
        {
            return connectionInfo;
        }

        public FormConnect()
        {
            InitializeComponent();
        }

        private void FormConnect_Load(object sender, EventArgs e)
        {
            AcceptButton = buttonConnect;

            textBoxServer.Text = RegistryHelper.GetString(Registry.CurrentUser, Constants.RegsitryKey, "", "ServerName", "localhost");
            textBoxUsername.Text = RegistryHelper.GetString(Registry.CurrentUser, Constants.RegsitryKey, "", "UserName", "administrator");
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            string verbatiumServername = textBoxServer.Text;
            string verbatiumUsername = textBoxUsername.Text;

            connectionInfo.ServerName = verbatiumServername.Trim();
            connectionInfo.UserName = verbatiumUsername.Trim();
            connectionInfo.Password = textBoxPassword.Text.Trim();
            connectionInfo.Port = Constants.DEFAULT_MANAGEMENT_PORT;

            int portBegin = connectionInfo.ServerName.IndexOf(':');
            if (portBegin > 0)
            {
                try
                {
                    connectionInfo.Port = int.Parse(connectionInfo.ServerName.Substring(portBegin + 1));
                    connectionInfo.ServerName = connectionInfo.ServerName.Substring(0, portBegin);
                }
                catch
                {
                    MessageBox.Show("The port number could not be parsed. Expected format \"ServerName\" or \"ServerName:Port\".", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return;
                }
            }

            if (TestConnection())
            {
                RegistryHelper.SetString(Registry.CurrentUser, Constants.RegsitryKey, "", "ServerName", verbatiumServername);
                RegistryHelper.SetString(Registry.CurrentUser, Constants.RegsitryKey, "", "UserName", verbatiumUsername);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show(connectMessage, Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
        }

        private bool TestConnection()
        {
            worker = new BackgroundWorker() { WorkerReportsProgress = true };
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerAsync();

            using (formProgress = new FormProgress())
            {
                if (formProgress.ShowDialog() == DialogResult.OK)
                {
                    return true;
                }
            }

            return false;
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            formProgress.UpdateStatus(e.UserState as ProgressFormStatus);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bool result = (bool)e.Result;

            if (result)
            {
                formProgress.Close(DialogResult.OK);
            }
            else
            {
                formProgress.Close(DialogResult.Cancel);
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = false;
            loginResult = false;
            connectMessage = "Failed to connect.";

            packeteer = new Packeteer();
            packeteer.OnMessageReceived += Packeteer_OnMessageReceived;

            try
            {
                worker.ReportProgress(0, new ProgressFormStatus() { Header = "Connecting..." });

                if (packeteer.Connect(connectionInfo.ServerName, connectionInfo.Port))
                {
                    worker.ReportProgress(0, new ProgressFormStatus() { Header = "Logging in..." });
                    loginConnectionEvent = new AutoResetEvent(false);

                    UserLogin userLogin = new UserLogin()
                    {
                        UserName = connectionInfo.UserName,
                        PasswordHash = Library.Crypto.Hashing.Sha256(connectionInfo.Password)
                    };

                    packeteer.SendAll(Constants.CommandLables.GUIRequestLogin, JsonConvert.SerializeObject(userLogin));

                    if (loginConnectionEvent.WaitOne(5000))
                    {
                        worker.ReportProgress(0, new ProgressFormStatus() { Header = "Logging in..." });

                        e.Result = loginResult;

                        if (loginResult == false)
                        {
                            connectMessage = "Unknown user or bad password.";
                        }
                    }
                }
                else
                {
                    connectMessage = "Could not connect to remote server.";
                }
            }
            catch (Exception ex)
            {
                connectMessage = "An error occured while logging in: " + ex.Message;
            }

            packeteer.Disconnect();
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, Hub.Common.Peer peer, Hub.Common.Packet packet)
        {
            if (packet.Label == Constants.CommandLables.GUIRequestLogin)
            {
                GenericBooleanResult result = JsonConvert.DeserializeObject<GenericBooleanResult>(packet.Payload);
                loginResult = result.Value;
                loginConnectionEvent.Set();
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void FormConnect_Shown(object sender, EventArgs e)
        {
            if (textBoxServer.Text.Length > 0 && textBoxUsername.Text.Length > 0)
            {
                textBoxPassword.Focus();
            }
        }
    }
}
