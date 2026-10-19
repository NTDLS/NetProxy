namespace NetProxy.Client
{
    /// <summary>
    /// What the console remembers between runs for the user who runs it (not including the login preferences).
    /// </summary>
    internal class UISettings
    {
        /// <summary>A culture name such as "de" or "pt-BR". Empty follows the Windows language.</summary>
        public string Language { get; set; } = string.Empty;

        public static UISettings Load()
        {
            try
            {
                return NTDLS.Persistence.LocalUserApplicationData.LoadFromDisk(NetProxy.Library.Constants.FriendlyName, new UISettings());
            }
            catch
            {
                return new UISettings();
            }
        }

        public void Save()
        {
            try
            {
                NTDLS.Persistence.LocalUserApplicationData.SaveToDisk(NetProxy.Library.Constants.FriendlyName, this);
            }
            catch
            {
            }
        }
    }
}
