using Minecraft_Server_Manager.Views;
using System.Windows;

namespace Minecraft_Server_Manager
{

    public partial class App : System.Windows.Application
    {

        private static Mutex _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "MinecraftServersManager_B12F6C80-5579-4B52-8C84-18D3A8576449";

            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                CustomMessageBox.Show("L'application est déjà en cours d'exécution !", "Minecraft Servers Manager");

                System.Windows.Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }

}
