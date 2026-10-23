using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NTDLS.Helpers;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// Adds or edits one plug-in of the service. The assembly is a file on the machine that the service runs on.
    /// </summary>
    public class FormPlugin : Form
    {
        private readonly NpPluginConfiguration _plugin;
        private readonly TextBox _name = new();
        private readonly TextBox _description = new();
        private readonly CheckBox _enabled = new();
        private readonly TextBox _assemblyPath = new();
        private readonly TextBox _typeName = new();
        private readonly TextBox _timeout = new();
        private readonly TextBox _settings = new();

        /// <summary>
        /// The plug-in as it was saved in the form. Valid when the dialog result is OK.
        /// </summary>
        public NpPluginConfiguration Result => _plugin;

        public FormPlugin(NpPluginConfiguration? plugin)
        {
            _plugin = plugin ?? new NpPluginConfiguration();

            Text = Localize(plugin == null ? "Add plugin" : "Edit plugin");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 450);

            var y = 12;
            AddRow(Localize("Name"), _name, ref y);
            AddRow(Localize("Description"), _description, ref y);

            _enabled.Text = Localize("Enabled");
            _enabled.Location = new Point(130, y);
            _enabled.AutoSize = true;
            Controls.Add(_enabled);
            y += 30;

            AddRow(Localize("Assembly (.dll)"), _assemblyPath, ref y, width: 330);
            var browse = new Button { Text = Localize("Browse..."), Location = new Point(468, y - 31), Size = new Size(80, 25) };
            browse.Click += BrowseAssembly;
            Controls.Add(browse);

            AddRow(Localize("Class (optional)"), _typeName, ref y);
            AddRow(Localize("Time limit (ms)"), _timeout, ref y, width: 100);

            Controls.Add(new Label { Text = Localize("Settings (one name=value per line)"), Location = new Point(12, y), AutoSize = true });
            y += 22;
            _settings.Multiline = true;
            _settings.ScrollBars = ScrollBars.Vertical;
            _settings.AcceptsReturn = true;
            _settings.Location = new Point(12, y);
            _settings.Size = new Size(536, 130);
            Controls.Add(_settings);
            y += 140;

            Controls.Add(new Label
            {
                Text = Localize("The assembly path is on the machine that the service runs on. A relative path is relative to the folder of the service."
                    + " The plugin reads its settings as typed here, so do not keep secrets in them."),
                Location = new Point(12, y),
                Size = new Size(536, 45)
            });

            var ok = new Button { Text = Localize("Save"), Location = new Point(392, 412), Size = new Size(75, 27), DialogResult = DialogResult.None };
            var cancel = new Button { Text = Localize("Cancel"), Location = new Point(473, 412), Size = new Size(75, 27), DialogResult = DialogResult.Cancel };
            ok.Click += Save;
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            _name.Text = _plugin.Name;
            _description.Text = _plugin.Description;
            _enabled.Checked = plugin == null || _plugin.Enabled;
            _assemblyPath.Text = _plugin.AssemblyPath;
            _typeName.Text = _plugin.TypeName;
            _timeout.Text = $"{_plugin.TimeoutMilliseconds}";
            _settings.Text = string.Join(Environment.NewLine, _plugin.Settings.Select(o => $"{o.Key}={o.Value}"));

            Theming.InitializeForm(this);
        }

        private void AddRow(string label, TextBox box, ref int y, int width = 418)
        {
            Controls.Add(new Label { Text = label, Location = new Point(12, y + 3), AutoSize = true });
            box.Location = new Point(130, y);
            box.Size = new Size(width, 23);
            Controls.Add(box);
            y += 31;
        }

        private void BrowseAssembly(object? sender, EventArgs e)
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
                _assemblyPath.Text = dialog.FileName;
            }
        }

        private void Save(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                Complain(Localize("The plugin name is required."));
                return;
            }

            if (string.IsNullOrWhiteSpace(_assemblyPath.Text))
            {
                Complain(Localize("The plugin assembly is required."));
                return;
            }

            if (int.TryParse(_timeout.Text, out var timeout) == false || timeout < 10 || timeout > 600000)
            {
                Complain(Localize("The time limit must be between 10 and 600000 milliseconds."));
                return;
            }

            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in _settings.Lines.Where(o => string.IsNullOrWhiteSpace(o) == false))
            {
                var equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    Complain(Localize("'{0}' is not a setting. Use name=value.", line));
                    return;
                }

                settings[line[..equals].Trim()] = line[(equals + 1)..].Trim();
            }

            _plugin.Name = _name.Text.Trim();
            _plugin.Description = _description.Text.Trim();
            _plugin.Enabled = _enabled.Checked;
            _plugin.AssemblyPath = _assemblyPath.Text.Trim();
            _plugin.TypeName = _typeName.Text.Trim();
            _plugin.TimeoutMilliseconds = timeout;
            _plugin.Settings = settings;

            DialogResult = DialogResult.OK;
            Close();
        }

        private static void Complain(string text)
            => MessageBox.Show(text, Constants.FriendlyName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
    }
}
