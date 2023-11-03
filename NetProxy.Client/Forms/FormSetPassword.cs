using NetProxy.Library;
using NetProxy.Library.Utilities;

namespace NetProxy.Client.Forms
{
    public partial class FormSetPassword : Form
    {
        public string PasswordHash
        {
            get => Utility.Sha256(textBoxPassword1.Text);
        }

        public FormSetPassword()
        {
            InitializeComponent();
        }

        private void buttonCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void buttonSave_Click(object? sender, EventArgs e)
        {
            if (textBoxPassword1.Text != textBoxPassword2.Text)
            {
                MessageBox.Show("The passwords do not match.", Constants.TitleCaption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            DialogResult = DialogResult.OK;
            this.Close();
        }

        private void FormSetPassword_Load(object? sender, EventArgs e)
        {
            AcceptButton = buttonSave;
            CancelButton = buttonCancel;
        }
    }
}
