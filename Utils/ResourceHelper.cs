using System.IO;
using System.Windows.Media.Imaging;

namespace Minecraft_Server_Manager.Utils
{
    public static class ResourceHelper
    {
        /// <summary>
        /// Récupère une chaîne de caractères depuis les ressources de l'application.
        /// </summary>
        public static string GetString(string key)
        {
            try
            {
                string rawString = System.Windows.Application.Current.FindResource(key) as string;

                if (rawString != null)
                {
                    return rawString.Replace("\\n", Environment.NewLine);
                }

                return $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }

        public static BitmapImage CreateImageFromBase64(string base64)
        {
            try
            {
                byte[] binaryData = Convert.FromBase64String(base64);
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(binaryData);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }
    }
}