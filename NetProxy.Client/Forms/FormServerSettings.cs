using NetProxy.Client.Classes;
using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using Newtonsoft.Json;

namespace NetProxy.Client.Forms
{
    public partial class FormServerSettings : Form
    {
        private Packeteer? _packeteer = null;

        private delegate void PopulateGrid(Users users);
        private PopulateGrid? _populateGrid = null;
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

            _populateGrid = OnPopulateGrid;

            _packeteer = LoginPacketeerFactory.GetNewPacketeer(connectionInfo);
            if (_packeteer == null)
            {
                Close();
                return;
            }
            _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
        }

        private void FormServerSettings_Shown(object? sender, EventArgs e)
        {
            Utility.EnsureNotNull(_packeteer);
            _packeteer.SendAll(Constants.CommandLables.GuiRequestUserList);
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, NetProxy.Hub.Common.Peer peer, NetProxy.Hub.Common.Packet packet)
        {
            if (packet.Label == Constants.CommandLables.GuiRequestUserList)
            {
                Utility.EnsureNotNull(_populateGrid);
                var collection = JsonConvert.DeserializeObject<Users>(packet.Payload);
                Utility.EnsureNotNull(collection);
                Invoke(_populateGrid, new object[] { collection });
            }
        }

        private void FormServerSettings_FormClosed(object? sender, FormClosedEventArgs e)
        {
            Utility.EnsureNotNull(_packeteer);
            _packeteer.Disconnect();
        }

        private void buttonSave_Click(object? sender, EventArgs e)
        {
            Users users = new Users();

            foreach (DataGridViewRow row in dataGridViewUsers.Rows)
            {
                if ((((string)row.Cells[ColumnUsername.Index].Value) ?? string.Empty) != string.Empty)
                {
                    string hash = (string)row.Cells[ColumnPassword.Index].Value;
                    if (hash == null || hash == string.Empty)
                    {
                        hash = Utility.Sha256(string.Empty);
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

            Utility.EnsureNotNull(_packeteer);
            _packeteer.SendAll(Constants.CommandLables.GuiPersistUserList, JsonConvert.SerializeObject(users));

            DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void dataGridViewUsers_CellClick(object? sender, DataGridViewCellEventArgs e)
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
