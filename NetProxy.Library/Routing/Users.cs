using System.Collections.Generic;

namespace NetProxy.Library.Routing
{
    public class Users
    {
        public List<User> List { get; set; }

        public Users()
        {
            List = new List<User>();
        }

        public void Add(User user)
        {
            List.Add(user);
        }
    }
}