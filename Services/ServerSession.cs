using Minecraft_Server_Manager.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Minecraft_Server_Manager.Services;

public enum ServerState { Stopped, Starting, Running, Stopping, RestartPending, Faulted }
public sealed record ServerSnapshot(ServerState State, bool IsBackingUp, ProcessMetrics Metrics,
    TimeSpan Uptime, string[] Players, string? Error = null)
{
    public bool IsActive => State is ServerState.Starting or ServerState.Running or ServerState.Stopping;
}

/// <summary>One owner per server. View navigation never changes the process lifetime.</summary>
public sealed class ServerSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly object _intentSync = new();
    private readonly object _playersSync = new();
    private readonly HashSet<string> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<Task<bool>> _acceptEula;
    private readonly Action<Action> _publish;
    private readonly TimeProvider _clock;
    private readonly List<CancellationTokenSource> _retiredIntents = new();
    private CancellationTokenSource _intent = new();
    private volatile ServerProcess? _run;
    private Task _observation = Task.CompletedTask;
    private volatile bool _shutdown;
    private volatile bool _desiredRunning;
    private volatile bool _ready;
    private bool _backingUp;
    private int _retryCount;
    private Task? _disposeTask;
    private DateOnly _lastScheduledRestart;
    private ServerSnapshot _snapshot = new(ServerState.Stopped, false, new(null, null, 0), TimeSpan.Zero, []);
    private const string LogPrefix = @"^(?:\[[^\]\r\n]+\]\s*)+:\s+";
    private static readonly Regex Join = new(LogPrefix + @"(\w+)(?:\[.*\]\s+logged in with| joined the game\s*$)", RegexOptions.Compiled);
    // The connection log uses the account name even when mods decorate the leave message.
    private static readonly Regex Leave = new(LogPrefix + @"(\w+) (?:left the game\s*$|lost connection:)", RegexOptions.Compiled);

    public ServerProfile Profile { get; }
    public LogBuffer Logs { get; } = new();
    // UI-thread-only history survives monitor navigation, without persisting commands to disk.
    internal List<string> CommandHistory { get; } = [];
    public ServerSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public event Action<string>? LogReceived;
    public event Action? LogsCleared;
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    public ServerSession(ServerProfile profile, Func<Task<bool>> acceptEula, Action<Action>? publish = null, TimeProvider? timeProvider = null)
    {
        Profile = profile; _acceptEula = acceptEula; _publish = publish ?? (action => action());
        _clock = timeProvider ?? TimeProvider.System;
    }

    private CancellationToken SetIntent(bool running)
    {
        lock (_intentSync)
        {
            if (running && _shutdown) throw new InvalidOperationException("L'application est en cours de fermeture.");
            _desiredRunning = running;
            _intent.Cancel(); _retiredIntents.Add(_intent); _intent = new();
            return _intent.Token;
        }
    }

    public async Task StartAsync()
    {
        if (_run != null && _desiredRunning) return;
        var token = SetIntent(true);
        await _lifecycle.WaitAsync(token).ConfigureAwait(false);
        try { _retryCount = 0; await StartRunAsync(token).ConfigureAwait(false); }
        finally { _lifecycle.Release(); }
    }

    private async Task StartRunAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_run != null) return;
        SetSnapshot(ServerState.Starting);
        try
        {
            var launch = ServerLauncher.Prepare(Profile);
            string eulaPath = Path.Combine(launch.WorkingDirectory, "eula.txt");
            bool accepted = File.Exists(eulaPath) && File.ReadLines(eulaPath)
                .Any(line => Regex.IsMatch(line, @"^\s*eula\s*=\s*true\s*$", RegexOptions.IgnoreCase));
            if (!accepted)
            {
                if (!await _acceptEula().ConfigureAwait(false))
                {
                    _desiredRunning = false; SetSnapshot(ServerState.Stopped); return;
                }
                token.ThrowIfCancellationRequested();
                AtomicFile.WriteAllText(eulaPath, "# Accepted through Minecraft Servers Manager\neula=true\n");
            }
            token.ThrowIfCancellationRequested();
            if (Profile.ClearLogsOnStart) { Logs.Clear(); LogsCleared?.Invoke(); }
            lock (_playersSync) _players.Clear();
            _ready = false;
            AddLog("[MSM] Démarrage du serveur.");
            var run = new ServerProcess(launch, Profile.LaunchMode == "Batch", AddLog);
            _run = run;
            _observation = ObserveAsync(run, token);
        }
        catch (OperationCanceledException) { SetSnapshot(ServerState.Stopped); throw; }
        catch (Exception ex)
        {
            SetSnapshot(ServerState.Faulted, error: ex.Message); AddLog("[MSM] " + ex.Message); throw;
        }
    }

    public async Task StopAsync()
    {
        SetIntent(false); // Cancels pending launch/retry BEFORE waiting for an operation.
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try { await StopRunAsync().ConfigureAwait(false); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopRunAsync()
    {
        var run = _run;
        if (run == null) { SetSnapshot(ServerState.Stopped); return; }
        SetSnapshot(ServerState.Stopping);
        AddLog("[MSM] Arrêt du serveur et sauvegarde du monde…");
        try
        {
            try { await SendCommandAsync("stop").ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
            { AddLog("[MSM] Console indisponible : " + ex.Message); }
            try { await run.Completion.WaitAsync(StopTimeout).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                AddLog("[MSM] Délai d'arrêt dépassé : arrêt forcé du groupe de processus.");
                run.Kill();
                await run.Completion.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
        }
        finally
        {
            // Release command writers before disposing their underlying streams.
            await _commands.WaitAsync().ConfigureAwait(false);
            try { _run = null; run.Dispose(); }
            finally { _commands.Release(); }
            lock (_playersSync) _players.Clear();
            SetSnapshot(ServerState.Stopped);
            _ = DiscordService.SendNotification(Profile.DiscordWebhookUrl, "Serveur arrêté", Profile.DisplayName, DiscordService.ColorRed);
        }
    }

    public async Task RestartAsync(TimeSpan? warning = null)
    {
        var token = SetIntent(true);
        await RestartWithIntentAsync(token, warning).ConfigureAwait(false);
    }

    private async Task RestartWithIntentAsync(CancellationToken token, TimeSpan? warning)
    {
        await _lifecycle.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (warning is { } delay && _run != null)
            {
                _ = DiscordService.SendNotification(Profile.DiscordWebhookUrl, "Redémarrage planifié", Profile.DisplayName, DiscordService.ColorOrange);
                await SendCommandAsync($"say Redémarrage dans {delay.TotalSeconds:0} secondes.", token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            await StopRunAsync().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            _retryCount = 0;
            await StartRunAsync(token).ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }

    public async Task SendCommandAsync(string command, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        if (command.Contains('\r') || command.Contains('\n')) throw new ArgumentException("Une seule commande par envoi.");
        await _commands.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var run = _run ?? throw new InvalidOperationException("Le serveur est arrêté.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await run.SendAsync(command, timeout.Token).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }

    public async Task<string> BackupAsync()
    {
        CancellationToken token;
        lock (_intentSync) token = _intent.Token;
        await _lifecycle.WaitAsync(token).ConfigureAwait(false);
        bool resume = _run != null && _desiredRunning;
        _backingUp = true;
        SetSnapshot(Snapshot.State);
        try
        {
            await StopRunAsync().ConfigureAwait(false);
            var path = await ServerBackup.CreateAsync(Profile.FolderPath, token).ConfigureAwait(false);
            AddLog("[MSM] Sauvegarde terminée : " + Path.GetFileName(path));
            return path;
        }
        catch (Exception ex) { AddLog("[MSM] Échec de sauvegarde : " + ex.Message); throw; }
        finally
        {
            try
            {
                if (resume && _desiredRunning && !token.IsCancellationRequested && !_shutdown)
                    await StartRunAsync(token).ConfigureAwait(false);
            }
            finally { _backingUp = false; SetSnapshot(Snapshot.State); _lifecycle.Release(); }
        }
    }

    public async Task ChangePermissionAsync(WhitelistPlayer player, bool op, bool remove)
    {
        if (!Regex.IsMatch(player.Name, @"^[A-Za-z0-9_]{1,16}$")) throw new ArgumentException("Nom de joueur invalide.");
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_run != null)
            {
                string command = op ? (remove ? "deop" : "op") : (remove ? "whitelist remove" : "whitelist add");
                await SendCommandAsync(command + " " + player.Name).ConfigureAwait(false);
                AddLog("[MSM] Commande envoyée : " + command + " " + player.Name);
            }
            else if (op) UpdateList("ops.json", new OpPlayer { Name = player.Name, Uuid = player.Uuid }, remove);
            else UpdateList("whitelist.json", player, remove);
        }
        finally { _lifecycle.Release(); }
    }
    private void UpdateList<T>(string filename, T player, bool remove) where T : WhitelistPlayer
    {
        string path = Path.Combine(Profile.FolderPath, filename);
        var items = File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? throw new JsonException("Liste invalide.") : [];
        items.RemoveAll(existing => existing.Uuid == player.Uuid);
        if (!remove) items.Add(player);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task ObserveAsync(ServerProcess run, CancellationToken intent)
    {
        bool notifiedReady = false;
        try
        {
            while (!run.Completion.IsCompleted)
            {
                // Observation belongs to the process, not to the current intent or view.
                await Task.WhenAny(run.Completion, Task.Delay(1000)).ConfigureAwait(false);
                await _lifecycle.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (run.Completion.IsCompleted || !ReferenceEquals(_run, run)) break;
                    ProcessMetrics metrics;
                    try { metrics = run.Sample(); }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                    { metrics = new(null, null, 0); AddLog("[MSM] Mesures indisponibles : " + ex.Message); }
                    var state = Snapshot.State;
                    if (_ready && state == ServerState.Starting) state = ServerState.Running;
                    SetSnapshot(state, metrics, run.Uptime);
                    if (_ready && !notifiedReady)
                    {
                        notifiedReady = true;
                        _ = DiscordService.SendNotification(Profile.DiscordWebhookUrl, "Serveur disponible", Profile.DisplayName, DiscordService.ColorGreen);
                    }
                    if (!_backingUp && !intent.IsCancellationRequested && _desiredRunning && Profile.AutoRestartEnabled &&
                        TimeOnly.TryParseExact(Profile.AutoRestartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduled))
                    {
                        var now = _clock.GetLocalNow();
                        if (now.Hour == scheduled.Hour && now.Minute == scheduled.Minute && _lastScheduledRestart != DateOnly.FromDateTime(now.DateTime))
                        {
                            _lastScheduledRestart = DateOnly.FromDateTime(now.DateTime);
                            _ = ReportOperationAsync(() => RestartWithIntentAsync(intent, TimeSpan.FromSeconds(10)));
                        }
                    }
                }
                finally { _lifecycle.Release(); }
            }
            int code = await run.Completion.ConfigureAwait(false);
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            bool retry = false;
            try
            {
                if (!ReferenceEquals(_run, run)) return;
                await _commands.WaitAsync().ConfigureAwait(false);
                try { _run = null; run.Dispose(); }
                finally { _commands.Release(); }
                lock (_playersSync) _players.Clear();
                AddLog($"[MSM] Processus terminé (code {code}).");
                if (run.Uptime > TimeSpan.FromMinutes(5)) _retryCount = 0;
                retry = _desiredRunning && !_shutdown && !intent.IsCancellationRequested && code != 0 && ++_retryCount <= 3;
                SetSnapshot(retry ? ServerState.RestartPending : code == 0 ? ServerState.Stopped : ServerState.Faulted);
                _ = DiscordService.SendNotification(Profile.DiscordWebhookUrl, code == 0 ? "Serveur arrêté" : "Arrêt inattendu", $"{Profile.DisplayName} (code {code})", DiscordService.ColorRed);
            }
            finally { _lifecycle.Release(); }
            if (retry)
            {
                AddLog($"[MSM] Relance {_retryCount}/3 en attente.");
                await Task.Delay(RetryDelay * _retryCount, intent).ConfigureAwait(false);
                await _lifecycle.WaitAsync(intent).ConfigureAwait(false);
                try { await StartRunAsync(intent).ConfigureAwait(false); }
                finally { _lifecycle.Release(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AddLog("[MSM] Surveillance : " + ex.Message);
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ReferenceEquals(_run, run))
                {
                    await _commands.WaitAsync().ConfigureAwait(false);
                    try { _run = null; run.Dispose(); }
                    finally { _commands.Release(); }
                    lock (_playersSync) _players.Clear();
                    SetSnapshot(ServerState.Faulted, error: ex.Message);
                }
            }
            finally { _lifecycle.Release(); }
        }
    }

    private async Task ReportOperationAsync(Func<Task> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AddLog("[MSM] " + ex.Message); }
    }

    public void AddLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 16384) text = text[..16384] + " [MSM: ligne tronquée pour l'affichage]";
        Logs.Append(text);
        if (text.Contains("Done (") && text.Contains("For help")) _ready = true;
        lock (_playersSync)
        {
            var join = Join.Match(text); if (join.Success) _players.Add(join.Groups[1].Value);
            var leave = Leave.Match(text); if (leave.Success) _players.Remove(leave.Groups[1].Value);
        }
        LogReceived?.Invoke(text);
    }

    private void SetSnapshot(ServerState state, ProcessMetrics? metrics = null, TimeSpan? uptime = null, string? error = null)
    {
        string[] players;
        lock (_playersSync) players = _players.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        bool active = state is ServerState.Starting or ServerState.Running or ServerState.Stopping;
        var previous = Snapshot;
        var snapshot = new ServerSnapshot(state, _backingUp, metrics ?? (active ? previous.Metrics : new(null, null, 0)),
            uptime ?? (active ? previous.Uptime : TimeSpan.Zero), players, error);
        Volatile.Write(ref _snapshot, snapshot);
        _publish(() => { Profile.IsRunning = snapshot.IsActive; Profile.IsBusy = snapshot.IsBackingUp || snapshot.State == ServerState.Stopping; Profile.PlayerCount = snapshot.Players.Length; });
    }

    public ValueTask DisposeAsync()
    {
        lock (_intentSync) return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        _shutdown = true;
        await StopAsync().ConfigureAwait(false);
        await _observation.ConfigureAwait(false);
        // No producer remains. Do not dispose locks while queued UI commands can still finish.
        lock (_intentSync) { _intent.Dispose(); foreach (var source in _retiredIntents) source.Dispose(); _retiredIntents.Clear(); }
    }
}
