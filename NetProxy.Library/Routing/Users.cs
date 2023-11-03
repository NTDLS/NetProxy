namespace NetProxy.Library.Routing
{
    public class Users
    {
        public List<User> Collection { get; set; } = new();

        public void Add(User user) => Collection.Add(user);
    }
}