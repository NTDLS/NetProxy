using NetProxy.Library.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy.Service
{
    public class Configuration
    {
        public int ManagementPort { get; set; }
        public Users Users = new Users();
    }
}
