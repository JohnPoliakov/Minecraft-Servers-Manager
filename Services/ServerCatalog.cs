using Minecraft_Server_Manager.Models;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Minecraft_Server_Manager.Services;

public static class AppPaths
{
    // Preserve existing catalogue locations independently of the install folder.
    public static string Profiles => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Minecraft Servers Manager");
    public static string BrowserData => Path.Combine(Profiles, "BrowserData");
}

public sealed class ServerCatalog(string? directory = null)
{
    private readonly string _directory = directory ?? AppPaths.Profiles;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public IReadOnlyList<ServerProfile> Load(Action<string> reportError)
    {
        var result = new List<ServerProfile>();
        if (!Directory.Exists(_directory)) return result;
        foreach (string path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<ServerProfile>(File.ReadAllText(path)) ?? throw new JsonException("Profil vide.");
                Validate(profile, checkFiles: false);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), profile.Id, StringComparison.OrdinalIgnoreCase))
                    throw new JsonException("L'identifiant ne correspond pas au fichier du profil.");
                if (result.Any(p => SameFolder(p.FolderPath, profile.FolderPath))) throw new JsonException("Dossier déjà enregistré.");
                result.Add(profile);
            }
            catch (Exception ex) { reportError($"{Path.GetFileName(path)} : {ex.Message}"); }
        }
        return result;
    }
    public void Save(ServerProfile profile)
    {
        Validate(profile, checkFiles: false);
        AtomicFile.WriteAllText(Path.Combine(_directory, profile.Id + ".json"), JsonSerializer.Serialize(profile, Json));
    }
    public void Delete(ServerProfile profile)
    {
        if (!Guid.TryParse(profile.Id, out _)) throw new InvalidDataException("Identifiant invalide.");
        File.Delete(Path.Combine(_directory, profile.Id + ".json"));
    }
    public static void Validate(ServerProfile profile, bool checkFiles = true)
    {
        if (profile.SchemaVersion != 1) throw new InvalidDataException("Version de profil non prise en charge.");
        if (!Guid.TryParse(profile.Id, out _) || string.IsNullOrWhiteSpace(profile.DisplayName)) throw new InvalidDataException("Nom ou identifiant invalide.");
        if (string.IsNullOrWhiteSpace(profile.FolderPath)) throw new InvalidDataException("Dossier manquant.");
        profile.FolderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(profile.FolderPath));
        if (profile.LaunchMode is not ("Java" or "Batch")) throw new InvalidDataException("Mode de lancement invalide.");
        if (profile.AutoRestartEnabled && !TimeOnly.TryParseExact(profile.AutoRestartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException("L'heure du redémarrage doit respecter HH:mm.");
        if (checkFiles) ServerLauncher.Prepare(profile);
    }
    public static bool SameFolder(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)), StringComparison.OrdinalIgnoreCase);
    public static ServerProfile Clone(ServerProfile profile) => JsonSerializer.Deserialize<ServerProfile>(JsonSerializer.Serialize(profile))!;
    public static void Apply(ServerProfile source, ServerProfile target)
    {
        target.DisplayName = source.DisplayName; target.FolderPath = source.FolderPath;
        target.JdkPath = source.JdkPath; target.JvmArguments = source.JvmArguments; target.JarName = source.JarName;
        target.IconBase64 = source.IconBase64; target.LaunchMode = source.LaunchMode; target.BatchFilename = source.BatchFilename;
        target.AutoRestartEnabled = source.AutoRestartEnabled; target.AutoRestartTime = source.AutoRestartTime;
        target.DiscordWebhookUrl = source.DiscordWebhookUrl; target.ClearLogsOnStart = source.ClearLogsOnStart;
    }
}
