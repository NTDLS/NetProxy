using NetProxy.Library.Payloads;
using NTDLS.Localization;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// The proxies of the service, ticked for the ones that use a plug-in, so that they can be chosen at once instead of one proxy at a time.
    /// </summary>
    public partial class FormPluginProxies : Form
    {
        private bool _updating;

        public FormPluginProxies(string pluginName, Guid pluginId, IEnumerable<NpProxyGridItem> proxies)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            Theming.InitializeForm(this);

            labelPrompt.Text = Localize("Choose the proxies that use the plugin '{0}'.", pluginName);

            //Items rather than DataSource: a data source is not bound until the list is on a form, so nothing could be ticked here.
            _updating = true;
            foreach (var proxy in proxies.OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                checkedListBoxProxies.Items.Add(new ProxyChoice(proxy), proxy.PluginIds.Contains(pluginId));
            }
            _updating = false;

            UpdateCheckAll();
        }

        /// <summary>
        /// The proxies that are ticked.
        /// </summary>
        public List<Guid> SelectedProxyIds => checkedListBoxProxies.CheckedItems.Cast<ProxyChoice>().Select(o => o.Id).ToList();

        private void UpdateCheckAll()
        {
            _updating = true;
            checkBoxCheckAll.Checked = checkedListBoxProxies.Items.Count > 0 && checkedListBoxProxies.CheckedItems.Count == checkedListBoxProxies.Items.Count;
            _updating = false;
        }

        private void CheckBoxCheckAll_CheckedChanged(object? sender, EventArgs e)
        {
            if (_updating)
            {
                return;
            }

            _updating = true;
            for (var i = 0; i < checkedListBoxProxies.Items.Count; i++)
            {
                checkedListBoxProxies.SetItemChecked(i, checkBoxCheckAll.Checked);
            }
            _updating = false;
        }

        private void CheckedListBoxProxies_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (_updating == false)
            {
                //The item is not ticked yet while this event is raised.
                BeginInvoke(UpdateCheckAll);
            }
        }

        private sealed class ProxyChoice(NpProxyGridItem proxy)
        {
            public Guid Id { get; } = proxy.Id;

            public override string ToString() => $"{proxy.Name} ({proxy.TrafficType}, :{proxy.ListenPort})";
        }
    }
}
