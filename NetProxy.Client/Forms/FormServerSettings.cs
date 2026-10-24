using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.Helpers;
using NTDLS.Localization;
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
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
        }

        public FormServerSettings(ConnectionInfo connectionInfo)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
            SetupToolTips();

            _populateGrid = OnPopulateGrid;

            comboBoxLogLevel.DataSource = Enum.GetValues<NpLogging.Severity>();

            _messageClient = MessageClientFactory.Create(connectionInfo, out var error);
            if (_messageClient == null)
            {
                MessageBox.Show(error ?? Localize("Could not connect to the service."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                Close();
                return;
            }
        }

        /// <summary>
        /// The headers of a grid are not translated by FormLocalizer.
        /// </summary>
        private void LocalizeGrids()
        {
            ColumnUsername.HeaderText = Localize("Username");
            ColumnSetPassword.HeaderText = Localize("Set Password");
            ColumnDescription.HeaderText = Localize("Description");
        }

        /// <summary>
        /// Tool-tips for the inputs: the label and the control that it is for show the same text, and so do the columns of the grid.
        /// </summary>
        private void SetupToolTips()
        {
            var toolTips = ToolTipHelpers.CreateToolTipControl(this);

            ColumnDescription.ToolTipText = Localize("Notes about this user.");

            toolTips.AddControls([labelLogLevel, comboBoxLogLevel],
                Localize("The least important kind of message that is logged. Verbose logs everything, including every connection. Information logs what the service does."
                + " Warning logs only problems. Exception logs only failures."));

            toolTips.AddControls([labelLogPath, textBoxLogPath],
                Localize("The log file, on the machine that the service runs on. A relative path is relative to the folder the service is installed in. Leave it empty to not write a file."));

            toolTips.AddControls([labelLogFileMaxSizeMB, textBoxLogFileMaxSizeMB],
                Localize("When the log file reaches this size in megabytes it is renamed to NetProxy.1.log and a new one is started (1 to 10240)."));

            toolTips.AddControls([labelLogFileRetainedCount, textBoxLogFileRetainedCount],
                Localize("How many of the older log files to keep, in addition to the current one (0 to 1000)."));

            toolTips.AddControls([checkBoxLogToEventLog],
                Localize("Also write warnings and exceptions to the Windows Application event log, which is where administrators and monitoring tools look. Not available on Linux."));

            toolTips.AddControls([checkBoxManagementListenOnIPv6],
                Localize("Let the management console connect over IPv6 as well as IPv4 (one listener accepts both). It takes effect when the service restarts."));
        }

        /// <summary>
        /// Picks the folder of the log file on the machine that the service runs on. The name of the file stays what it is.
        /// </summary>
        private void ButtonBrowseLogPath_Click(object? sender, EventArgs e)
        {
            if (_messageClient == null)
            {
                return;
            }

            var current = textBoxLogPath.Text.Trim();

            using var form = new FormRemoteBrowser(_messageClient, true, [], current, Localize("Select the folder of the log file"));
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            //The file name of the current path is kept, and the path is built the way the service machine writes them.
            var fileName = current.Length > 0 ? current.Split('/', '\\').Last() : string.Empty;
            if (fileName.Length == 0 || fileName.Contains('.') == false)
            {
                fileName = "NetProxy.log";
            }

            var folder = form.SelectedPath;
            var separator = folder.Contains('\\') ? '\\' : '/';
            textBoxLogPath.Text = folder.EndsWith(separator) ? folder + fileName : folder + separator + fileName;
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
                    MessageBox.Show(Localize("The new user '{0}' needs a password. Use [Set Password].", userName), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
                MessageBox.Show(Localize("There must be at least one user, otherwise nobody could log in."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
                        Localize("The log file roll-over size (MB) must be between [min] and [max].")),
                    LogFileRetainedCount = textBoxLogFileRetainedCount.GetAndValidateNumeric(0, 1000,
                        Localize("The number of rolled log files to keep must be between [min] and [max].")),
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
