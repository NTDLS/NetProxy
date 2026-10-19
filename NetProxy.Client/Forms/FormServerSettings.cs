using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.Helpers;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    public partial class FormServerSettings : Form
    {
        private delegate void PopulateGrid(List<NpUser> users);

        private readonly RmClient? _messageClient = null;
        private readonly PopulateGrid? _populateGrid = null;

        /// <summary>
        /// The users the service has. Their passwords stay on the service: a user that is saved without a new password keeps its own.
        /// </summary>
        private readonly HashSet<string> _existingUsers = new(StringComparer.CurrentCultureIgnoreCase);

        private NpServiceSettings _settings = new();

        private void OnPopulateGrid(List<NpUser> users)
        {
            foreach (var user in users)
            {
                object[] values = new object[5];
                values[ColumnId.Index] = user.Id;
                values[ColumnUsername.Index] = user.UserName;
                values[ColumnDescription.Index] = user.Description;

                var index = dataGridViewUsers.Rows.Add(values);

                //Changing the name would make it a new user. Delete the row and add another to do that.
                dataGridViewUsers.Rows[index].Cells[ColumnUsername.Index].ReadOnly = true;
                _existingUsers.Add(user.UserName);
            }
        }

        public FormServerSettings()
        {
            InitializeComponent();
            Theming.InitializeForm(this);
            GridTheming.Apply(this);
        }

        public FormServerSettings(ConnectionInfo connectionInfo)
        {
            InitializeComponent();
            Theming.InitializeForm(this);
            GridTheming.Apply(this);

            _populateGrid = OnPopulateGrid;

            comboBoxLogLevel.DataSource = Enum.GetValues<NpLogging.Severity>();

            _messageClient = MessageClientFactory.Create(connectionInfo, out var error);
            if (_messageClient == null)
            {
                MessageBox.Show(error ?? "Could not connect to the service.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                Close();
                return;
            }
        }

        private void FormServerSettings_Shown(object? sender, EventArgs e)
        {
            if (_messageClient == null)
            {
                return;
            }

            var result = _messageClient.Query<QueryUserListReply>(new QueryUserList());
            Invoke(_populateGrid.EnsureNotNull(), [result.Collection]);

            _settings = _messageClient.Query<QueryServiceSettingsReply>(new QueryServiceSettings()).Settings;

            comboBoxLogLevel.SelectedItem = _settings.LogLevel;
            textBoxLogPath.Text = _settings.LogPath;
            textBoxLogFileMaxSizeMB.Text = $"{_settings.LogFileMaxSizeMB}";
            textBoxLogFileRetainedCount.Text = $"{_settings.LogFileRetainedCount}";
            checkBoxLogToEventLog.Checked = _settings.LogToEventLog;
            checkBoxManagementListenOnIPv6.Checked = _settings.ManagementListenOnIPv6;
        }

        private void FormServerSettings_FormClosed(object? sender, FormClosedEventArgs e)
        {
            _messageClient?.Disconnect();
        }

        private void ButtonSave_Click(object? sender, EventArgs e)
        {
            var users = new List<NpUser>();

            foreach (DataGridViewRow row in dataGridViewUsers.Rows)
            {
                var userName = ((string?)row.Cells[ColumnUsername.Index].Value) ?? string.Empty;
                if (row.IsNewRow || userName.Trim() == string.Empty)
                {
                    continue;
                }

                //Only a password that was just set has a hash. The service keeps the password of everyone else.
                var passwordHash = (string?)row.Cells[ColumnPassword.Index].Value ?? string.Empty;

                if (passwordHash == string.Empty && _existingUsers.Contains(userName) == false)
                {
                    tabControl1.SelectedTab = tabPageUsers;
                    MessageBox.Show($"The new user '{userName}' needs a password. Use [Set Password].", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return;
                }

                users.Add(new NpUser
                {
                    Id = (string?)row.Cells[ColumnId.Index].Value ?? string.Empty,
                    UserName = userName.Trim(),
                    PasswordHash = passwordHash,
                    Description = (string?)row.Cells[ColumnDescription.Index].Value ?? string.Empty
                });
            }

            if (users.Count == 0)
            {
                tabControl1.SelectedTab = tabPageUsers;
                MessageBox.Show("There must be at least one user, otherwise nobody could log in.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            NpServiceSettings settings;

            try
            {
                settings = new NpServiceSettings
                {
                    LogLevel = comboBoxLogLevel.SelectedItem is NpLogging.Severity level ? level : _settings.LogLevel,
                    LogPath = textBoxLogPath.Text.Trim(),
                    LogFileMaxSizeMB = textBoxLogFileMaxSizeMB.GetAndValidateNumeric(1, 10240,
                        "The log file roll-over size (MB) must be between [min] and [max]."),
                    LogFileRetainedCount = textBoxLogFileRetainedCount.GetAndValidateNumeric(0, 1000,
                        "The number of rolled log files to keep must be between [min] and [max]."),
                    LogToEventLog = checkBoxLogToEventLog.Checked,
                    ManagementListenOnIPv6 = checkBoxManagementListenOnIPv6.Checked
                };
            }
            catch (Exception ex)
            {
                tabControl1.SelectedTab = tabPageService;
                MessageBox.Show(ex.Message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            _messageClient.EnsureNotNull().Notify(new NotificationPersistUserList(users));
            _messageClient.Notify(new NotificationPersistServiceSettings(settings));

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void DataGridViewUsers_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == ColumnSetPassword.Index)
            {
                var row = dataGridViewUsers.Rows[e.RowIndex];
                if (row != null && !string.IsNullOrEmpty(row.Cells[ColumnUsername.Index].Value?.ToString()))
                {
                    using var formSetPassword = new FormSetPassword();
                    if (formSetPassword.ShowDialog() == DialogResult.OK)
                    {
                        row.Cells[ColumnPassword.Index].Value = formSetPassword.PasswordHash;
                    }
                }
            }
        }
    }
}
