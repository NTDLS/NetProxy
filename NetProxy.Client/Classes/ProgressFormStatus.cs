namespace NetProxy.Client
{
    public class ProgressFormStatus
    {
        public string Caption { get; set; }
        public string Header { get; set; }
        public string Body { get; set; }
        public int? ProgressValue { get; set; }
        public int? ProgressMinimum { get; set; }
        public int? ProgressMaximum { get; set; }

        public ProgressFormStatus()
        {
            Caption = null;
            Header = null;
            Body = null;
            ProgressValue = null;
            ProgressMinimum = null;
            ProgressMaximum = null;
        }
    }
}
