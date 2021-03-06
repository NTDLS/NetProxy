﻿using System;
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
        private ConnectionInfo _connectionInfo = new ConnectionInfo();
        private FormProgress _formProgress = null;
        private AutoResetEvent _loginConnectionEvent = null;
        private BackgroundWorker _worker = null;
        private string _connectMessage = string.Empty;
        private bool _loginResult = false;
        private Packeteer _packeteer = null;

        public ConnectionInfo GetConnectionInfo()
        {
            return _connectionInfo;
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

            _connectionInfo.ServerName = verbatiumServername.Trim();
            _connectionInfo.UserName = verbatiumUsername.Trim();
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
                RegistryHelper.SetString(Registry.CurrentUser, Constants.RegsitryKey, "", "ServerName", verbatiumServername);
                RegistryHelper.SetString(Registry.CurrentUser, Constants.RegsitryKey, "", "UserName", verbatiumUsername);
                this.DialogResult = DialogResult.OK;
                this.Close();
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

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            _formProgress.UpdateStatus(e.UserState as ProgressFormStatus);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bool result = (bool)e.Result;

            if (result)
            {
                _formProgress.Close(DialogResult.OK);
            }
            else
            {
                _formProgress.Close(DialogResult.Cancel);
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = false;
            _loginResult = false;
            _connectMessage = "Failed to connect.";

            _packeteer = new Packeteer();
            _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;

            try
            {
                _worker.ReportProgress(0, new ProgressFormStatus() { Header = "Connecting..." });

                if (_packeteer.Connect(_connectionInfo.ServerName, _connectionInfo.Port))
                {
                    _worker.ReportProgress(0, new ProgressFormStatus() { Header = "Logging in..." });
                    _loginConnectionEvent = new AutoResetEvent(false);

                    UserLogin userLogin = new UserLogin()
                    {
                        UserName = _connectionInfo.UserName,
                        PasswordHash = Library.Crypto.Hashing.Sha256(_connectionInfo.Password)
                    };

                    _packeteer.SendAll(Constants.CommandLables.GuiRequestLogin, JsonConvert.SerializeObject(userLogin));

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
                else
                {
                    _connectMessage = "Could not connect to remote server.";
                }
            }
            catch (Exception ex)
            {
                _connectMessage = "An error occured while logging in: " + ex.Message;
            }

            _packeteer.Disconnect();
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, Hub.Common.Peer peer, Hub.Common.Packet packet)
        {
            if (packet.Label == Constants.CommandLables.GuiRequestLogin)
            {
                GenericBooleanResult result = JsonConvert.DeserializeObject<GenericBooleanResult>(packet.Payload);
                _loginResult = result.Value;
                _loginConnectionEvent.Set();
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
