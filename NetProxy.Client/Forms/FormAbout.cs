using NetProxy.Library;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

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
                this.ShowInTaskbar = true;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.TopMost = true;
            }
            else
            {
                this.ShowInTaskbar = false;
                this.StartPosition = FormStartPosition.CenterParent;
                this.TopMost = false;
            }
        }

        private void FormAbout_Load(object sender, EventArgs e)
        {
            this.AcceptButton = cmdOk;
            this.CancelButton = cmdOk;
            this.Text = "NetworkDLS " + Constants.TitleCaption + " : About";

            var files = Directory.EnumerateFiles(Path.GetDirectoryName(_assembly.Location), "*.dll", SearchOption.AllDirectories).ToList();
            files.AddRange(Directory.EnumerateFiles(Path.GetDirectoryName(_assembly.Location), "*.exe", SearchOption.AllDirectories).ToList());

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
                listViewVersions.Items.Add(new ListViewItem(new string[] { componentAssembly.Name, componentAssembly.Version.ToString() }));
            }
            catch
            {
            }
        }

        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.NetworkDLS.com");
        }
    }
}
