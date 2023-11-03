using System.ComponentModel;

namespace NetProxy.Service
{
    [RunInstaller(true)]
    public partial class NetProxyServiceInstaller : System.Configuration.Install.Installer
    {
        public NetProxyServiceInstaller()
        {
            InitializeComponent();
        }
    }
}
