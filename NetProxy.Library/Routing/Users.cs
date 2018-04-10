using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy.Library.Routing
{
    public class Users
    {
        public List<User> List = new List<User>();

        public void Add(User user)
        {
            List.Add(user);
        }
    }
}
