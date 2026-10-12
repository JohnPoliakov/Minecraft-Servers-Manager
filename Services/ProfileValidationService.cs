using Minecraft_Server_Manager.Models;
using System.IO;

namespace Minecraft_Server_Manager.Services
{
    /// <summary>
    /// Valide les données d'un ServerProfile avant le lancement du serveur.
    /// </summary>
    public static class ProfileValidationService
    {
        /// <summary>
        /// Valide le profil et retourne une liste de messages d'erreur.
        /// Retourne une liste vide si tout est valide.
        /// </summary>
        public static List<string> Validate(ServerProfile profile)
        {
            var errors = new List<string>();

            // --- Validation du dossier serveur ---
            if (string.IsNullOrWhiteSpace(profile.FolderPath))
            {
                errors.Add("Le dossier du serveur n'est pas configuré.");
            }
            else if (!Directory.Exists(profile.FolderPath))
            {
                errors.Add($"Le dossier du serveur est introuvable :\n{profile.FolderPath}");
            }

            // --- Validation selon le mode de lancement ---
            if (profile.LaunchMode == "Batch")
            {
                // Mode Batch : vérifier le script .bat
                if (string.IsNullOrWhiteSpace(profile.BatchFilename))
                {
                    errors.Add("Aucun fichier script (.bat) n'est configuré.");
                }
                else if (!string.IsNullOrWhiteSpace(profile.FolderPath))
                {
                    string batPath = Path.Combine(profile.FolderPath, profile.BatchFilename);
                    if (!File.Exists(batPath))
                    {
                        errors.Add($"Le script de démarrage est introuvable :\n{batPath}");
                    }
                }
            }
            else
            {
                // Mode Java direct : vérifier le .jar
                if (string.IsNullOrWhiteSpace(profile.JarName))
                {
                    errors.Add("Aucun fichier .jar n'est configuré.");
                }
                else if (!string.IsNullOrWhiteSpace(profile.FolderPath))
                {
                    string jarPath = Path.Combine(profile.FolderPath, profile.JarName);
                    if (!File.Exists(jarPath))
                    {
                        errors.Add($"Le fichier .jar est introuvable :\n{jarPath}");
                    }
                }

                // --- Validation du chemin Java ---
                if (!string.IsNullOrWhiteSpace(profile.JdkPath) && profile.JdkPath != "java")
                {
                    if (!File.Exists(profile.JdkPath))
                    {
                        errors.Add($"Le chemin Java est invalide :\n{profile.JdkPath}");
                    }
                }
            }

            return errors;
        }
    }
}
