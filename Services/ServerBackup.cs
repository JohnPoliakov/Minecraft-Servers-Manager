using System.IO;
using System.IO.Compression;

namespace Minecraft_Server_Manager.Services;

/// <summary>Archives a stopped server; a single unreadable file fails the entire backup.</summary>
public static class ServerBackup
{
    public static Task<string> CreateAsync(string folder, CancellationToken token = default) => Task.Run(() =>
    {
        folder = Path.GetFullPath(folder);
        string destination = Path.Combine(folder, "Backups");
        Directory.CreateDirectory(destination);
        string archivePath = Path.Combine(destination, $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid():N}.zip");
        string temporary = archivePath + ".partial";
        try
        {
            int count = 0;
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (string file in Enumerate(folder, destination))
                {
                    token.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(file, Path.GetRelativePath(folder, file), CompressionLevel.Fastest);
                    count++;
                }
            }
            if (count == 0) throw new IOException("Aucun fichier à sauvegarder.");
            using (var verify = ZipFile.OpenRead(temporary))
            {
                if (verify.Entries.Count != count) throw new IOException("Archive incomplète.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, archivePath);
            // Retention only after publication. The newly completed backup is always retained.
            foreach (var old in new DirectoryInfo(destination).EnumerateFiles("Backup_*.zip"))
            {
                if (old.FullName != archivePath && old.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7))
                {
                    try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            return archivePath;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }, token);

    private static IEnumerable<string> Enumerate(string folder, string excluded)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(folder))
        {
            if (string.Equals(path, excluded, StringComparison.OrdinalIgnoreCase)) continue;
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException($"Lien de fichiers non pris en charge : {path}");
            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (string file in Enumerate(path, excluded)) yield return file;
            }
            else if (Path.GetFileName(path) != "session.lock") yield return path;
        }
    }
}
