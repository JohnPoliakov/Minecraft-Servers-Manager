using Minecraft_Server_Manager.Models;
using System.IO;
using System.IO.Compression;

namespace Minecraft_Server_Manager.Services;

public static class ServerInstaller
{
    public static bool IsTrustedUri(string value, bool image = false)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort) return false;
        bool forgeCdn = uri.Host.Equals("forgecdn.net", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".forgecdn.net", StringComparison.OrdinalIgnoreCase);
        return forgeCdn || (!image && (uri.Host.Equals("curseforge.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".curseforge.com", StringComparison.OrdinalIgnoreCase)));
    }

    public static Task<ServerProfile> InstallAsync(string zipPath, string installRoot, int ram, CancellationToken token,
        long maximumBytes = 20L * 1024 * 1024 * 1024) => Task.Run(async () =>
    {
        installRoot = Path.GetFullPath(installRoot);
        Directory.CreateDirectory(installRoot);
        string staging = Path.Combine(installRoot, ".msm-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                if (zip.Entries.Count > 100000) throw new InvalidDataException("L'archive contient trop de fichiers.");
                long total = 0;
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    total = checked(total + entry.Length);
                    if (total > maximumBytes) throw new InvalidDataException("L'archive dépasse la taille maximale décompressée.");
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Les liens symboliques ne sont pas pris en charge.");
                    string target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                    if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Chemin d'archive invalide.");
                    if (entry.FullName.Contains(':')) throw new InvalidDataException("Chemin d'archive invalide.");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using var input = entry.Open();
                    await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                    byte[] buffer = new byte[81920];
                    long written = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                    {
                        written += read;
                        if (written > entry.Length) throw new InvalidDataException("Taille d'entrée invalide.");
                        await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    }
                    if (written != entry.Length) throw new InvalidDataException("Archive incomplète.");
                }
            }
            string root = staging;
            while (!Directory.EnumerateFiles(root).Any() && Directory.GetDirectories(root) is { Length: 1 } children) root = children[0];
            string name = Path.GetFileNameWithoutExtension(zipPath);
            var profile = new ServerProfile { DisplayName = name, FolderPath = root, JvmArguments = $"-Xmx{ram}G -Xms1G" };
            DetectLaunch(profile);
            token.ThrowIfCancellationRequested();
            string final = Path.Combine(installRoot, name);
            if (Directory.Exists(final) || File.Exists(final)) final += "_" + Guid.NewGuid().ToString("N")[..8];
            Directory.Move(root, final);
            profile.FolderPath = final;
            return profile;
        }
        finally
        {
            // Only the uniquely created staging directory is owned by this operation.
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }, token);

    public static void DetectLaunch(ServerProfile profile)
    {
        var files = Directory.GetFiles(profile.FolderPath);
        var scripts = files.Where(file => Path.GetExtension(file).ToLowerInvariant() is ".bat" or ".cmd")
            .OrderBy(file => Path.GetFileName(file).Equals("run.bat", StringComparison.OrdinalIgnoreCase) ? 0 :
                Path.GetFileName(file).Contains("start", StringComparison.OrdinalIgnoreCase) ? 1 : 2).ThenBy(file => file, StringComparer.OrdinalIgnoreCase).ToArray();
        if (scripts.Length > 0)
        {
            profile.LaunchMode = "Batch"; profile.BatchFilename = Path.GetFileName(scripts[0]); return;
        }
        var jars = files.Where(file => Path.GetExtension(file).Equals(".jar", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetFileName(file).Contains("installer", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => Path.GetFileName(file).Equals("server.jar", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(file => file).ToArray();
        if (jars.Length == 0) throw new InvalidDataException("Aucun script de lancement ou JAR serveur détecté. Installez les fichiers serveur du pack.");
        profile.LaunchMode = "Java"; profile.JarName = Path.GetFileName(jars[0]);
    }
}
