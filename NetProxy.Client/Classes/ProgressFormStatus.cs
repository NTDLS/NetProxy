namespace NetProxy.Client
{
    public class ProgressFormStatus
    {
        public string Caption { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public int? ProgressValue { get; set; }
        public int? ProgressMinimum { get; set; }
        public int? ProgressMaximum { get; set; }

        public ProgressFormStatus()
        {
            ProgressValue = null;
            ProgressMinimum = null;
            ProgressMaximum = null;
        }
    }
}
