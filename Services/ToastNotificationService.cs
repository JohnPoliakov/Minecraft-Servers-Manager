using Microsoft.Toolkit.Uwp.Notifications;

namespace Minecraft_Server_Manager.Services
{
    /// <summary>
    /// Service de notifications toast Windows natives.
    /// Remplace les BalloonTips WinForms par de vraies notifications système.
    /// </summary>
    public static class ToastNotificationService
    {
        /// <summary>
        /// Affiche une notification toast Windows native.
        /// </summary>
        /// <param name="title">Titre de la notification.</param>
        /// <param name="message">Corps du message.</param>
        public static void Show(string title, string message)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch
            {
                // Silencieux si les toast ne sont pas supportés
            }
        }

        /// <summary>
        /// Nettoie l'historique des notifications au shutdown de l'application.
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                ToastNotificationManagerCompat.History.Clear();
            }
            catch { }
        }
    }
}
