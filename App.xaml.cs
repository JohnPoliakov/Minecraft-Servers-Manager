using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.Views;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;

namespace Minecraft_Server_Manager
{

    public partial class App : System.Windows.Application
    {

        private static Mutex _mutex = null;

        /// <summary>
        /// Identifiant unique de l'application pour Windows (toast notifications, taskbar grouping).
        /// </summary>
        public const string AppUserModelId = "ArcanicFactory.MinecraftServersManager";

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Log l'erreur
            System.Diagnostics.Debug.WriteLine($"Exception non gérée : {e.Exception.Message}\n{e.Exception.StackTrace}");

            // Affiche l'erreur avant que l'app ne meure
            System.Windows.MessageBox.Show($"Une erreur inattendue est survenue :\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                            "Crash Application",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

            // Ne pas avaler les erreurs fatales qui corrompent l'état de l'application
            // Seules les erreurs non critiques sont récupérables
            bool isFatal = e.Exception is OutOfMemoryException
                        || e.Exception is StackOverflowException
                        || e.Exception is System.Threading.ThreadAbortException
                        || e.Exception is AccessViolationException;

            e.Handled = !isFatal;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Définir l'AppUserModelID avant toute création de fenêtre
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

            const string appName = "MinecraftServersManager_B12F6C80-5579-4B52-8C84-18D3A8576449";

            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                CustomMessageBox.Show("L'application est déjà en cours d'exécution !", "Minecraft Servers Manager");

                System.Windows.Application.Current.Shutdown();
                return;
            }

            // Créer le raccourci Start Menu avec l'AppUserModelID
            // Nécessaire pour que Windows affiche la bonne icône dans les toast notifications
            EnsureStartMenuShortcut();

            base.OnStartup(e);
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Nettoyer les toast notifications au shutdown
            ToastNotificationService.Cleanup();
            base.OnExit(e);
        }

        /// <summary>
        /// Crée un raccourci .lnk dans le Start Menu avec l'AppUserModelID et l'icône de l'application.
        /// Windows utilise ce raccourci pour résoudre l'icône affichée dans les toast notifications.
        /// </summary>
        private static void EnsureStartMenuShortcut()
        {
            try
            {
                string shortcutPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Minecraft Servers Manager.lnk");

                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                // Si le raccourci existe déjà, ne pas le recréer
                if (System.IO.File.Exists(shortcutPath)) return;

                // Créer le raccourci via COM (IShellLink)
                var shellLink = (IShellLinkW)new CShellLink();

                shellLink.SetPath(exePath);
                shellLink.SetWorkingDirectory(System.IO.Path.GetDirectoryName(exePath) ?? "");
                shellLink.SetDescription("Minecraft Servers Manager");
                shellLink.SetIconLocation(exePath, 0);

                // Définir l'AppUserModelID via IPropertyStore
                var propertyStore = (IPropertyStore)shellLink;

                var appIdKey = new PROPERTYKEY
                {
                    fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                    pid = 5
                };

                var propVariant = new PROPVARIANT
                {
                    vt = 31, // VT_LPWSTR
                    pwszVal = Marshal.StringToCoTaskMemUni(AppUserModelId)
                };

                propertyStore.SetValue(ref appIdKey, ref propVariant);
                propertyStore.Commit();

                Marshal.FreeCoTaskMem(propVariant.pwszVal);

                // Sauvegarder le fichier .lnk
                var persistFile = (IPersistFile)shellLink;
                persistFile.Save(shortcutPath, true);

                Marshal.ReleaseComObject(shellLink);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Impossible de créer le raccourci Start Menu : {ex.Message}");
            }
        }

        #region Native COM Interop

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }

        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out ushort pwHotkey);
            void SetHotkey(ushort wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            int Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPVARIANT
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr pwszVal;
        }

        #endregion
    }

}
