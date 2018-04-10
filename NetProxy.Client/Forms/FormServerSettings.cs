using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Routing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetProxy.Hub;

namespace NetProxy.Client.Forms
{
    public partial class FormServerSettings : Form
    {
        private ConnectionInfo connectionInfo = null;
        private Packeteer packeteer = null;

        private delegate void PopulateGrid(Users users);
        private PopulateGrid populateGrid = null;
        private void OnPopulateGrid(Users users)
        {
            foreach (var user in users.List)
            {
                object[] values = new object[5];
                values[ColumnId.Index] = user.Id;
                values[ColumnUsername.Index] = user.UserName;
                values[ColumnPassword.Index] = user.PasswordHash;
                values[ColumnDescription.Index] = user.Description;

                dataGridViewUsers.Rows.Add(values);
            }
        }

        public FormServerSettings()
        {
            InitializeComponent();
        }

        public FormServerSettings(ConnectionInfo connectionInfo)
        {
            InitializeComponent();

            populateGrid = OnPopulateGrid;
            this.connectionInfo = connectionInfo;

            packeteer = LoginPacketeerFactory.GetNewPacketeer(connectionInfo);
            packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
        }


        private void FormServerSettings_Shown(object sender, EventArgs e)
        {
            packeteer.SendAll(Constants.CommandLables.GUIRequestUserList);
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, NetProxy.Hub.Common.Peer peer, NetProxy.Hub.Common.Packet packet)
        {
            if (packet.Label == Constants.CommandLables.GUIRequestUserList)
            {
                var collection = JsonConvert.DeserializeObject<Users>(packet.Payload);
                this.Invoke(populateGrid, new object[] { collection });
            }
        }

        private void FormServerSettings_FormClosed(object sender, FormClosedEventArgs e)
        {
            packeteer.Disconnect();
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            Users users = new Users();

            foreach (DataGridViewRow row in dataGridViewUsers.Rows)
            {
                if((((string)row.Cells[ColumnUsername.Index].Value) ?? string.Empty) != string.Empty)
                {
                    string hash = (string)row.Cells[ColumnPassword.Index].Value;
                    if (hash == null || hash == string.Empty)
                    {
                        hash = Library.Crypto.Hashing.Sha256(string.Empty);
                    }

                    users.Add(new User
                    {
                        Id = (string)row.Cells[ColumnId.Index].Value,
                        UserName = (string)row.Cells[ColumnUsername.Index].Value,
                        PasswordHash = (string)row.Cells[ColumnPassword.Index].Value,
                        Description = (string)row.Cells[ColumnDescription.Index].Value
                    });
                }
            }

            packeteer.SendAll(Constants.CommandLables.GUIPersistUserList, JsonConvert.SerializeObject(users) );

            DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void dataGridViewUsers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == ColumnSetPassword.Index)
            {
                var row = dataGridViewUsers.Rows[e.RowIndex];
                if (row != null)
                {
                    using (var formSetPassword = new FormSetPassword())
                    {
                        if (formSetPassword.ShowDialog() == DialogResult.OK)
                        {
                            row.Cells[ColumnPassword.Index].Value = formSetPassword.PasswordHash;
                        }
                    }
                }
            }
        }
    }
}
