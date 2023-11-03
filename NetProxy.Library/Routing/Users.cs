namespace NetProxy.Library.Routing
{
    public class Users
    {
        public List<User> List { get; set; } = new();

        public void Add(User user) => List.Add(user);
    }
}