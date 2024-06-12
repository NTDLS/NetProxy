using NetProxy.Client.Classes;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.NullExtensions;
using NTDLS.ReliableMessaging;

namespace NetProxy.Client.Forms
{
    public partial class FormServerSettings : Form
    {
        private readonly RmClient? _messageClient = null;

        private delegate void PopulateGrid(List<NpUser> users);
        private readonly PopulateGrid? _populateGrid = null;
        private void OnPopulateGrid(List<NpUser> users)
        {
            foreach (var user in users)
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

            _messageClient = MessageClientFactory.Create(connectionInfo);
            if (_messageClient == null)
            {
                Close();
                return;
            }
        }

        private void FormServerSettings_Shown(object? sender, EventArgs e)
        {
            _messageClient.EnsureNotNull().Query<QueryUserListReply>(new QueryUserList()).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result?.Collection != null)
                {
                    Invoke(_populateGrid.EnsureNotNull(), new object[] { t.Result.Collection });
                }
            });
        }

        private void FormServerSettings_FormClosed(object? sender, FormClosedEventArgs e)
        {
            _messageClient.EnsureNotNull().Disconnect();
        }

        private void buttonSave_Click(object? sender, EventArgs e)
        {
            var users = new List<NpUser>();

            foreach (DataGridViewRow row in dataGridViewUsers.Rows)
            {
                if ((((string)row.Cells[ColumnUsername.Index].Value) ?? string.Empty) != string.Empty)
                {
                    string passwordHash = (string)row.Cells[ColumnPassword.Index].Value;
                    if (string.IsNullOrEmpty(passwordHash) == false)
                    {
                        passwordHash = NpUtility.Sha256(string.Empty);
                    }

                    users.Add(new NpUser
                    {
                        Id = (string)row.Cells[ColumnId.Index].Value,
                        UserName = (string)row.Cells[ColumnUsername.Index].Value,
                        PasswordHash = passwordHash,
                        Description = (string)row.Cells[ColumnDescription.Index].Value
                    });
                }
            }

            _messageClient.EnsureNotNull().Notify(new NotificationPersistUserList(users));

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
