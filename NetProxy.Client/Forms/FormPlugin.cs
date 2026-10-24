using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Localization;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// Adds or edits one plug-in of the service. The assembly is a file on the machine that the service runs on.
    /// </summary>
    public partial class FormPlugin : Form
    {
        private readonly NpPluginConfiguration _plugin = new();

        /// <summary>
        /// The plug-in as it was saved in the form. Valid when the dialog result is OK.
        /// </summary>
        public NpPluginConfiguration Result => _plugin;

        public FormPlugin() : this(null)
        {
        }

        public FormPlugin(NpPluginConfiguration? plugin)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            Theming.InitializeForm(this);

            _plugin = plugin ?? new NpPluginConfiguration();

            Text = Localize(plugin == null ? "Add plugin" : "Edit plugin");

            textBoxName.Text = _plugin.Name;
            textBoxDescription.Text = _plugin.Description;
            checkBoxEnabled.Checked = plugin == null || _plugin.Enabled;
            textBoxAssemblyPath.Text = _plugin.AssemblyPath;
            textBoxTypeName.Text = _plugin.TypeName;
            textBoxTimeout.Text = $"{_plugin.TimeoutMilliseconds}";
            textBoxSettings.Text = string.Join(Environment.NewLine, _plugin.Settings.Select(o => $"{o.Key}={o.Value}"));
        }

        private void ButtonBrowse_Click(object? sender, EventArgs e)
        {
            //This browses the machine that the console runs on. The service reads the path, so it only makes sense to browse
            //  when that is the same machine; otherwise type the path on the service machine.
            using var dialog = new OpenFileDialog
            {
                Title = Localize("Select the plugin assembly"),
                Filter = Localize("Assembly (*.dll)|*.dll|All files (*.*)|*.*")
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textBoxAssemblyPath.Text = dialog.FileName;
            }
        }

        private void ButtonSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxName.Text))
            {
                Complain(Localize("The plugin name is required."));
                return;
            }

            if (string.IsNullOrWhiteSpace(textBoxAssemblyPath.Text))
            {
                Complain(Localize("The plugin assembly is required."));
                return;
            }

            if (int.TryParse(textBoxTimeout.Text, out var timeout) == false || timeout < 10 || timeout > 600000)
            {
                Complain(Localize("The time limit must be between 10 and 600000 milliseconds."));
                return;
            }

            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in textBoxSettings.Lines.Where(o => string.IsNullOrWhiteSpace(o) == false))
            {
                var equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    Complain(Localize("'{0}' is not a setting. Use name=value.", line));
                    return;
                }

                settings[line[..equals].Trim()] = line[(equals + 1)..].Trim();
            }

            _plugin.Name = textBoxName.Text.Trim();
            _plugin.Description = textBoxDescription.Text.Trim();
            _plugin.Enabled = checkBoxEnabled.Checked;
            _plugin.AssemblyPath = textBoxAssemblyPath.Text.Trim();
            _plugin.TypeName = textBoxTypeName.Text.Trim();
            _plugin.TimeoutMilliseconds = timeout;
            _plugin.Settings = settings;

            DialogResult = DialogResult.OK;
            Close();
        }

        private static void Complain(string text)
            => MessageBox.Show(text, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
    }
}
