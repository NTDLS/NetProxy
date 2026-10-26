using NetProxy.Client.Classes;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Helpers;
using NTDLS.Localization;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// The TLS certificates of the service: add, edit and delete them, see when they expire and which proxies use them. A proxy refers
    /// to a certificate by its name.
    /// </summary>
    public partial class FormCertificates : Form
    {
        private readonly RmClient? _messageClient;
        private List<NpCertificateGridItem> _certificates = new();

        public FormCertificates()
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);
        }

        public FormCertificates(ConnectionInfo connectionInfo)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            LocalizeGrids();
            Theming.InitializeForm(this);

            _messageClient = MessageClientFactory.Create(connectionInfo, out var error);
            if (_messageClient == null)
            {
                MessageBox.Show(error ?? Localize("Could not connect to the service."), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                Load += (_, _) => Close();
                return;
            }

            Shown += (_, _) => RefreshList();
            FormClosed += (_, _) => _messageClient?.Disconnect();
        }

        /// <summary>
        /// The headers of a grid are not translated by FormLocalizer.
        /// </summary>
        private void LocalizeGrids()
        {
            ColumnName.HeaderText = Localize("Name");
            ColumnExpires.HeaderText = Localize("Expires");
            ColumnDaysLeft.HeaderText = Localize("Days left");
            ColumnUsedBy.HeaderText = Localize("Used by");
            ColumnFile.HeaderText = Localize("File");
            ColumnSubject.HeaderText = Localize("Subject");
        }

        private NpCertificateGridItem? Selected
            => dataGridViewCertificates.CurrentRow != null && dataGridViewCertificates.CurrentRow.Index < _certificates.Count
                ? _certificates[dataGridViewCertificates.CurrentRow.Index] : null;

        private void RefreshList(Guid? select = null)
        {
            if (_messageClient == null)
            {
                return;
            }

            _certificates = _messageClient.Query<QueryCertificateListReply>(new QueryCertificateList()).Collection
                .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

            dataGridViewCertificates.Rows.Clear();
            foreach (var certificate in _certificates)
            {
                var index = dataGridViewCertificates.Rows.Add(
                    certificate.Name,
                    certificate.NotAfterUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-",
                    certificate.DaysLeft?.ToString() ?? "-",
                    string.Join(", ", certificate.UsedBy),
                    certificate.Path,
                    certificate.IsLoaded ? certificate.Subject : $"{Localize("Not loaded")}: {certificate.Error}");

                var row = dataGridViewCertificates.Rows[index];
                row.Cells[ColumnSubject.Index].ToolTipText = certificate.IsLoaded ? certificate.Subject : certificate.Error;
                row.Cells[ColumnFile.Index].ToolTipText = certificate.Path;
                row.Cells[ColumnUsedBy.Index].ToolTipText = string.Join(Environment.NewLine, certificate.UsedBy);

                if (certificate.IsLoaded == false || certificate.DaysLeft < 14)
                {
                    row.DefaultCellStyle.ForeColor = Color.Firebrick;
                }

                if (select == certificate.Id)
                {
                    dataGridViewCertificates.CurrentCell = row.Cells[ColumnName.Index];
                }
            }
        }

        private void DataGridViewCertificates_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                Edit();
            }
        }

        private void ButtonAdd_Click(object? sender, EventArgs e) => EditAndSave(null);

        private void ButtonEdit_Click(object? sender, EventArgs e) => Edit();

        private void ButtonRefresh_Click(object? sender, EventArgs e) => RefreshList(Selected?.Id);

        /// <summary>
        /// Shows the editor and saves what is entered. If the service does not accept it (for example the name is taken) it says why and
        /// the editor comes back with what was entered, so that nothing has to be typed again. Cancelling ends it.
        /// </summary>
        private void EditAndSave(NpCertificateConfiguration? certificate)
        {
            while (true)
            {
                using var form = new FormCertificate(certificate, _messageClient);
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                QueryUpsertCertificateReply reply;

                try
                {
                    reply = _messageClient.EnsureNotNull().Query<QueryUpsertCertificateReply>(new QueryUpsertCertificate { Certificate = form.Result, MatchByName = false });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Localize("The certificate was not saved: {0}", ex.GetBaseException().Message), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    certificate = form.Result;
                    continue;
                }

                if (reply.Success == false)
                {
                    MessageBox.Show(Localize("The certificate was not saved. {0}", reply.Message ?? string.Empty), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    certificate = form.Result;
                    continue;
                }

                RefreshList(form.Result.Id);

                if (string.IsNullOrEmpty(reply.LoadError) == false)
                {
                    MessageBox.Show(Localize("The certificate was saved but its file could not be loaded: {0}", reply.LoadError), Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }

                return;
            }
        }

        private void Edit()
        {
            if (Selected is not { } selected)
            {
                return;
            }

            //A copy, so that cancelling leaves the list as it was.
            EditAndSave(new NpCertificateConfiguration
            {
                Id = selected.Id,
                Name = selected.Name,
                Description = selected.Description,
                Path = selected.Path,
                Password = selected.Password
            });
        }

        private void ButtonDelete_Click(object? sender, EventArgs e)
        {
            if (Selected is not { } selected)
            {
                return;
            }

            if (selected.UsedBy.Count > 0)
            {
                MessageBox.Show(Localize("The certificate '{0}' is used by {1}. Change those proxies first.", selected.Name, string.Join(", ", selected.UsedBy)),
                    Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (MessageBox.Show(Localize("Delete the certificate '{0}'?", selected.Name), Constants.FriendlyName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                var reply = _messageClient.EnsureNotNull().Query<QueryActionReply>(new QueryCertificateAction { Id = selected.Id, Action = NpCertificateAction.Delete });

                RefreshList();

                if (reply.Success == false && string.IsNullOrEmpty(reply.Message) == false)
                {
                    MessageBox.Show(reply.Message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetBaseException().Message, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }
    }
}
