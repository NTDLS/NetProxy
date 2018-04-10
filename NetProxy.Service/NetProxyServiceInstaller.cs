using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;

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
