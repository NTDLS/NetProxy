using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Localization;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// Adds or edits one certificate of the service. The PFX file is on the machine that the service runs on.
    /// </summary>
    public partial class FormCertificate : Form
    {
        private readonly NpCertificateConfiguration _certificate;
        private readonly RmClient? _messageClient;

        /// <summary>
        /// The certificate as it was saved in the form. Valid when the dialog result is OK.
        /// </summary>
        public NpCertificateConfiguration Result => _certificate;

        public FormCertificate() : this(null)
        {
        }

        /// <param name="certificate">The certificate to edit, or null to add one.</param>
        /// <param name="messageClient">The connection to the service, to browse for the file on the machine that it runs on.</param>
        public FormCertificate(NpCertificateConfiguration? certificate, RmClient? messageClient = null)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            Theming.InitializeForm(this);

            _messageClient = messageClient;
            _certificate = certificate ?? new NpCertificateConfiguration();

            Text = Localize(certificate == null ? "Add certificate" : "Edit certificate");

            textBoxName.Text = _certificate.Name;
            textBoxDescription.Text = _certificate.Description;
            textBoxPath.Text = _certificate.Path;
            textBoxPassword.Text = _certificate.Password;
        }

        private void ButtonBrowse_Click(object? sender, EventArgs e)
        {
            //The service reads the file, so this browses the machine that the service runs on.
            if (_messageClient == null)
            {
                return;
            }

            using var form = new FormRemoteBrowser(_messageClient, false, [".pfx", ".p12"], textBoxPath.Text, Localize("Select the TLS certificate"));
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                textBoxPath.Text = form.SelectedPath;

                if (string.IsNullOrWhiteSpace(textBoxName.Text))
                {
                    textBoxName.Text = Path.GetFileNameWithoutExtension(form.SelectedPath.Replace('\\', '/'));
                }
            }
        }

        private void ButtonSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxName.Text))
            {
                Complain(Localize("The certificate name is required."));
                return;
            }

            if (string.IsNullOrWhiteSpace(textBoxPath.Text))
            {
                Complain(Localize("The certificate file is required."));
                return;
            }

            _certificate.Name = textBoxName.Text.Trim();
            _certificate.Description = textBoxDescription.Text.Trim();
            _certificate.Path = textBoxPath.Text.Trim();
            _certificate.Password = textBoxPassword.Text;

            DialogResult = DialogResult.OK;
            Close();
        }

        private static void Complain(string text)
            => MessageBox.Show(text, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
    }
}
