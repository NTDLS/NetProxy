using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy.Service.Routing
{
    public class Packet
    {
        public byte[] Buffer { get; set; }
        public int Length { get; set; }
    }
}
