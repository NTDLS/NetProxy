namespace NetProxy.Client.Classes
{
    public class ComboItem(string display, object value)
    {
        public string Display { get; set; } = display;
        public object Value { get; set; } = value;
    }
}
