using System.Diagnostics;
using System.IO;
using Minecraft_Server_Manager.Models;

namespace Minecraft_Server_Manager.Services;

public static class ServerLauncher
{
    public static ProcessStartInfo Prepare(ServerProfile profile)
    {
        string folder = Path.GetFullPath(profile.FolderPath);
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        string java = string.IsNullOrWhiteSpace(profile.JdkPath) ? "java" : profile.JdkPath;
        var info = new ProcessStartInfo
        {
            WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (profile.LaunchMode == "Batch")
        {
            string batch = Path.GetFullPath(Path.Combine(folder, profile.BatchFilename));
            if (!File.Exists(batch)) throw new FileNotFoundException("Script introuvable.", batch);
            if (Path.GetExtension(batch).ToLowerInvariant() is not (".bat" or ".cmd"))
                throw new InvalidOperationException("Le lancement batch nécessite un fichier .bat ou .cmd.");
            if (batch.Contains('%') || batch.Contains('"') || batch.Contains('\r') || batch.Contains('\n'))
                throw new InvalidOperationException("Le chemin du script contient un caractère non pris en charge par cmd.exe.");
            info.FileName = "cmd.exe";
            // The initial stdin gate lets the owner assign the job BEFORE any children start.
            info.Arguments = $"/d /q /v:off /s /c \"set /p MSM_START= & call \"{batch}\"\"";
            if (File.Exists(java))
            {
                string bin = Path.GetDirectoryName(Path.GetFullPath(java))!;
                info.Environment["JAVA_HOME"] = Directory.GetParent(bin)?.FullName ?? bin;
                info.Environment["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            }
        }
        else
        {
            string jar = Path.GetFullPath(Path.Combine(folder, profile.JarName));
            if (!File.Exists(jar)) throw new FileNotFoundException("JAR introuvable.", jar);
            info.FileName = java;
            info.Arguments = $"{profile.JvmArguments} -jar \"{jar}\" nogui";
        }
        return info;
    }
}

public sealed class ServerProcess : IDisposable
{
    private readonly Process _process;
    private readonly ProcessGroup _group;
    private readonly Task _stdout;
    private readonly Task _stderr;
    public Task<int> Completion { get; }
    public DateTime StartedUtc { get; } = DateTime.UtcNow;
    private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
    public TimeSpan Uptime => Stopwatch.GetElapsedTime(_startedTimestamp);

    public ServerProcess(ProcessStartInfo info, bool releaseBatchGate, Action<string> onLog)
    {
        _group = new ProcessGroup();
        _process = new Process { StartInfo = info };
        try
        {
            _process.Start();
            _group.Assign(_process);
            _stdout = ReadAsync(_process.StandardOutput, onLog);
            _stderr = ReadAsync(_process.StandardError, onLog);
            if (releaseBatchGate) { _process.StandardInput.WriteLine("start"); _process.StandardInput.Flush(); }
            Completion = CompleteAsync();
        }
        catch
        {
            try { if (!_process.HasExited) _process.Kill(true); } catch (InvalidOperationException) { }
            _group.Dispose(); _process.Dispose();
            throw;
        }
    }

    public ProcessMetrics Sample() => _group.Sample();
    public async Task SendAsync(string command, CancellationToken token)
    {
        await _process.StandardInput.WriteLineAsync(command.AsMemory(), token).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync(token).ConfigureAwait(false);
    }
    private static async Task ReadAsync(StreamReader reader, Action<string> onLog)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) onLog(line);
    }
    private async Task<int> CompleteAsync()
    {
        await _process.WaitForExitAsync().ConfigureAwait(false);
        int exitCode = _process.ExitCode;
        // A detached child still belongs to this job, even after cmd exits.
        while (_group.ActiveProcessCount > 0) await Task.Delay(100).ConfigureAwait(false);
        await Task.WhenAll(_stdout, _stderr).ConfigureAwait(false);
        return exitCode;
    }
    public void Kill() => _group.Kill();
    public void Dispose() { _group.Dispose(); _process.Dispose(); }
}
