using Minecraft_Server_Manager.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace Minecraft_Server_Manager.Services;

public sealed class ServerConfiguration
{
    private readonly string _path;
    private string _original;
    private static readonly Regex Property = new(@"^\s*([^#!\s][^=]*?)\s*=(.*)$", RegexOptions.Compiled);
    public ServerConfiguration(string folder)
    {
        _path = Path.Combine(folder, "server.properties");
        _original = File.Exists(_path) ? File.ReadAllText(_path) : "";
    }
    public IReadOnlyList<PropertyItem> Read() => _original.Split('\n').Select(line => Property.Match(line.TrimEnd('\r')))
        .Where(match => match.Success).Select(match => new PropertyItem { Key = match.Groups[1].Value.Trim(), Value = match.Groups[2].Value }).ToArray();
    public void Save(IEnumerable<PropertyItem> items)
    {
        string current = File.Exists(_path) ? File.ReadAllText(_path) : "";
        if (current != _original) throw new IOException("server.properties a changé sur disque. Rouvrez l'éditeur avant d'enregistrer.");
        var values = items.GroupBy(i => i.Key).ToDictionary(g => g.Key, g => g.Last().Value);
        var lines = _original.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Property.Match(lines[i].TrimEnd('\r'));
            if (!match.Success || !values.TryGetValue(match.Groups[1].Value.Trim(), out string? value) || value == match.Groups[2].Value) continue;
            if (value.Contains('\n') || value.Contains('\r')) throw new InvalidDataException("Une propriété doit tenir sur une ligne.");
            if (lines[i].TrimEnd('\r').EndsWith('\\') || (i > 0 && lines[i - 1].TrimEnd('\r').EndsWith('\\')))
                throw new InvalidDataException("Modifiez les propriétés multilignes avec un éditeur de texte.");
            lines[i] = match.Groups[1].Value.Trim() + "=" + value + (lines[i].EndsWith('\r') ? "\r" : "");
        }
        string updated = string.Join('\n', lines);
        AtomicFile.WriteAllText(_path, updated);
        _original = updated;
    }
}
