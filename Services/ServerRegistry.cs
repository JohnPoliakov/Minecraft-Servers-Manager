using Minecraft_Server_Manager.Models;

namespace Minecraft_Server_Manager.Services;

public sealed class ServerRegistry(Func<Task<bool>> acceptEula, Action<Action>? publish = null)
{
    private readonly Dictionary<string, ServerSession> _sessions = new();
    private Task? _shutdown;
    public ServerSession Get(ServerProfile profile)
    {
        if (_shutdown != null) throw new InvalidOperationException("L'application est en cours de fermeture.");
        if (!_sessions.TryGetValue(profile.Id, out var session))
            _sessions.Add(profile.Id, session = new ServerSession(profile, acceptEula, publish));
        return session;
    }
    public async Task RemoveAsync(ServerProfile profile)
    {
        if (_sessions.TryGetValue(profile.Id, out var session))
        {
            await session.DisposeAsync();
            _sessions.Remove(profile.Id);
        }
    }
    public Task ShutdownAsync() => _shutdown ??= Task.WhenAll(_sessions.Values.Select(s => s.DisposeAsync().AsTask()));
}
