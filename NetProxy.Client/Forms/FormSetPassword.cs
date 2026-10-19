using NetProxy.Library;
using NetProxy.Library.Utilities;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    public partial class FormSetPassword : Form
    {
        public string PasswordHash
        {
            get => NpUtility.Sha256(textBoxPassword1.Text);
        }

        public FormSetPassword()
        {
            InitializeComponent();
            Theming.InitializeForm(this);
            SetupToolTips();
        }

        private void SetupToolTips()
        {
            var toolTips = ToolTipHelpers.CreateToolTipControl(this);

            toolTips.AddControls([label3, textBoxPassword1],
                "The new password. Only a hash of it is sent to the service.");

            toolTips.AddControls([label1, textBoxPassword2],
                "Type the password again, to be sure that it is what you meant.");
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ButtonSave_Click(object? sender, EventArgs e)
        {
            if (textBoxPassword1.Text != textBoxPassword2.Text)
            {
                MessageBox.Show("The passwords do not match.", Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (textBoxPassword1.Text.Length == 0 && MessageBox.Show("An empty password lets anyone log in as this user. Use it anyway?", Constants.FriendlyName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void FormSetPassword_Load(object? sender, EventArgs e)
        {
            AcceptButton = buttonSave;
            CancelButton = buttonCancel;
        }
    }
}
