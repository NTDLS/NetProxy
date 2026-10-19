using NetProxy.Client.Forms;
using NTDLS.Localization;
using NTDLS.WinFormsHelpers;

namespace NetProxy.Client
{
    class Program
    {
        [STAThreadAttribute]
        static void Main(string[] args)
        {
            //The language is the user's choice in the settings, or else the Windows language.
            Localizer.Default.SetLanguage(UISettings.Load().Language);

            ApplicationConfiguration.Initialize();
            Theming.InitializeApplication(); //Follows the Windows light/dark setting.
            Application.Run(new FormMain());
        }
    }
}
