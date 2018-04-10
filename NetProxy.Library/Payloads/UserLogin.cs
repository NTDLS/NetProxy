using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy.Library.Payloads
{
    public class UserLogin
    {
        public string UserName { get; set; }
        public string PasswordHash { get; set; }
    }
}
