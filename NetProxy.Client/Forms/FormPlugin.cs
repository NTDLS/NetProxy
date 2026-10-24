using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Localization;
using NTDLS.ReliableMessaging;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// Adds or edits one plug-in of the service. The assembly is a file on the machine that the service runs on.
    /// </summary>
    public partial class FormPlugin : Form
    {
        private readonly NpPluginConfiguration _plugin = new();
        private readonly RmClient? _messageClient;

        /// <summary>
        /// The plug-in as it was saved in the form. Valid when the dialog result is OK.
        /// </summary>
        public NpPluginConfiguration Result => _plugin;

        public FormPlugin() : this(null)
        {
        }

        /// <param name="plugin">The plugin to edit, or null to add one.</param>
        /// <param name="messageClient">The connection to the service, to browse for the assembly on the machine that it runs on.</param>
        public FormPlugin(NpPluginConfiguration? plugin, RmClient? messageClient = null)
        {
            _messageClient = messageClient;

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
            //The service reads the path, so this browses the machine that the service runs on.
            if (_messageClient == null)
            {
                return;
            }

            using var form = new FormRemoteBrowser(_messageClient, false, [".dll"], textBoxAssemblyPath.Text, Localize("Select the plugin assembly"));
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                textBoxAssemblyPath.Text = form.SelectedPath;
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
