using Minecraft_Server_Manager.Models;
using System.Diagnostics;
using System.IO;

namespace Minecraft_Server_Manager.Services
{
    /// <summary>
    /// Gère les fichiers PID pour permettre le rattachement aux processus orphelins
    /// après un crash de l'application MSM.
    /// </summary>
    public static class PidFileService
    {
        /// <summary>
        /// Retourne le chemin du fichier PID pour un profil donné.
        /// </summary>
        private static string GetPidFilePath(ServerProfile profile)
        {
            string pidDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Minecraft Servers Manager",
                ".pids");

            Directory.CreateDirectory(pidDir);
            return Path.Combine(pidDir, $"{profile.Id}.pid");
        }

        /// <summary>
        /// Écrit le PID du processus dans un fichier associé au profil.
        /// </summary>
        public static void WritePidFile(ServerProfile profile, int processId)
        {
            try
            {
                string path = GetPidFilePath(profile);
                File.WriteAllText(path, processId.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PID] Erreur écriture PID pour {profile.DisplayName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Supprime le fichier PID associé au profil.
        /// </summary>
        public static void DeletePidFile(ServerProfile profile)
        {
            try
            {
                string path = GetPidFilePath(profile);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PID] Erreur suppression PID pour {profile.DisplayName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Tente de rattacher un processus orphelin au profil.
        /// Retourne true si un processus a été rattaché.
        /// </summary>
        public static bool TryReattachOrphanProcess(ServerProfile profile)
        {
            try
            {
                string path = GetPidFilePath(profile);
                if (!File.Exists(path)) return false;

                string pidText = File.ReadAllText(path).Trim();
                if (!int.TryParse(pidText, out int pid)) 
                {
                    File.Delete(path);
                    return false;
                }

                Process process;
                try
                {
                    process = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    // Le processus n'existe plus — nettoyer le fichier PID obsolète
                    File.Delete(path);
                    return false;
                }

                // Vérifier que c'est bien un processus java/cmd (pas un processus recyclé sans rapport)
                string processName = process.ProcessName.ToLowerInvariant();
                if (processName is not ("java" or "javaw" or "cmd"))
                {
                    process.Dispose();
                    File.Delete(path);
                    return false;
                }

                // Rattacher le processus au profil
                profile.ServerProcess = process;
                profile.IsRunning = true;

                Debug.WriteLine($"[PID] Processus orphelin rattaché: {profile.DisplayName} (PID: {pid})");
                profile.AddLog($">>> [RECOVERY] Processus serveur retrouvé (PID: {pid}). Monitoring repris.");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PID] Erreur rattachement pour {profile.DisplayName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Nettoie tous les fichiers PID obsolètes (processus qui n'existent plus).
        /// </summary>
        public static void CleanupStalePidFiles()
        {
            try
            {
                string pidDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Minecraft Servers Manager",
                    ".pids");

                if (!Directory.Exists(pidDir)) return;

                foreach (var file in Directory.GetFiles(pidDir, "*.pid"))
                {
                    try
                    {
                        string pidText = File.ReadAllText(file).Trim();
                        if (int.TryParse(pidText, out int pid))
                        {
                            try
                            {
                                Process.GetProcessById(pid);
                                // Le processus existe encore, ne pas supprimer
                            }
                            catch (ArgumentException)
                            {
                                // Le processus n'existe plus
                                File.Delete(file);
                            }
                        }
                        else
                        {
                            File.Delete(file);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
