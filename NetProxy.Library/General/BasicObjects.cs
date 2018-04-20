namespace NetProxy.Library.General
{
    public class ComboItem
    {
        public string Display { get; set; }
        public object Value { get; set; }
        public ComboItem(string display, object value)
        {
            this.Display = display;
            this.Value = value;
        }
    }
}
