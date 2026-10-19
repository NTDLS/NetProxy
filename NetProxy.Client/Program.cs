using NTDLS.WinFormsHelpers;

namespace NetProxy.Client
{
    class Program
    {
        [STAThreadAttribute]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Theming.InitializeApplication(); //Follows the Windows light/dark setting.
            Application.Run(new Forms.FormMain());
        }
    }
}
