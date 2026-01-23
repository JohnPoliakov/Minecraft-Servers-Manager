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
    }
}