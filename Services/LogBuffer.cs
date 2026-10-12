namespace Minecraft_Server_Manager.Services;

public sealed class LogBuffer(int maxLines = 5000, int maxCharacters = 1024 * 1024)
{
    private readonly object _sync = new();
    private readonly Queue<string> _lines = new();
    private int _characters;
    public void Append(string text)
    {
        if (text.Length > maxCharacters) text = text[^maxCharacters..];
        lock (_sync)
        {
            _lines.Enqueue(text); _characters += text.Length;
            while (_lines.Count > maxLines || _characters > maxCharacters) _characters -= _lines.Dequeue().Length;
        }
    }
    public string[] Snapshot(int limit = 1000) { lock (_sync) return _lines.TakeLast(limit).ToArray(); }
    public void Clear() { lock (_sync) { _lines.Clear(); _characters = 0; } }
}
