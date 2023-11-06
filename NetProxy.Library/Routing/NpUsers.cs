namespace NetProxy.Library.Routing
{
    public class NpUsers
    {
        public List<NpUser> Collection { get; set; } = new();

        public void Add(NpUser user) => Collection.Add(user);
    }
}