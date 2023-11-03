using NetProxy.Library;
using System.Reflection;

namespace NetProxy.Client.Forms
{
    public partial class FormAbout : Form
    {
        Assembly _assembly = Assembly.GetExecutingAssembly();

        public FormAbout()
        {
            InitializeComponent();
        }

        public FormAbout(bool showInTaskbar)
        {
            InitializeComponent();

            if (showInTaskbar)
            {
                ShowInTaskbar = true;
                StartPosition = FormStartPosition.CenterScreen;
                TopMost = true;
            }
            else
            {
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.CenterParent;
                TopMost = false;
            }
        }

        private void FormAbout_Load(object? sender, EventArgs e)
        {
            AcceptButton = cmdOk;
            CancelButton = cmdOk;
            Text = "NetworkDLS " + Constants.TitleCaption + " : About";

            var files = Directory.EnumerateFiles(Path.GetDirectoryName(_assembly.Location) ?? "", "*.dll", SearchOption.AllDirectories).ToList();
            files.AddRange(Directory.EnumerateFiles(Path.GetDirectoryName(_assembly.Location) ?? "", "*.exe", SearchOption.AllDirectories).ToList());

            foreach (var file in files)
            {
                AddApplication(file);
            }

            listViewVersions.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            listViewVersions.AutoResizeColumn(1, ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void AddApplication(string appPath)
        {
            try
            {
                AssemblyName componentAssembly = AssemblyName.GetAssemblyName(appPath);
                var assembly = Assembly.Load(componentAssembly);
                var companyAttribute = Attribute.GetCustomAttribute(assembly, typeof(AssemblyCompanyAttribute)) as AssemblyCompanyAttribute;

                if (companyAttribute?.Company.ToLower().Contains("networkdls") == false)
                {
                    return;
                }

                listViewVersions.Items.Add(new ListViewItem(new string[] { componentAssembly?.Name ?? "", componentAssembly?.Version?.ToString() ?? "" }));
            }
            catch
            {
            }
        }

        private void linkLabel_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.NetworkDLS.com");
        }
    }
}
