using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy.Service.Routing
{
    public class StickySession
    {
        public string DestinationAddress { get; set; }
        public int DestinationPort { get; set; }
    }
}
