namespace NetProxy.Library.General
{
    public class ComboItem
    {
        public string Display { get; set; }
        public object Value { get; set; }
        public ComboItem(string display, object value)
        {
            Display = display;
            Value = value;
        }
    }
}
