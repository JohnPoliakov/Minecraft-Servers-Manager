using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.UserControls;
using Minecraft_Server_Manager.ViewModels;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

internal static class Program
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "MSM-Regression-" + Guid.NewGuid().ToString("N"));
    [STAThread] private static int Main(string[] args)
    {
        try { return Execute(args); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static int Execute(string[] args)
    {
        if (args.Contains("--server")) return Worker();
        Directory.CreateDirectory(Root);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources["MC-Font"] = new FontFamily("Consolas");
        app.Resources["IntToVisibilityConverter"] = new Minecraft_Server_Manager.Converters.IntToVisibilityConverter();
        app.Resources["BoolToVis"] = new Minecraft_Server_Manager.Converters.BooleanToVisibilityConverter();
        ThemeResources.Apply("Dark Blue"); ThemeResources.ApplyLanguage("en-US");
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var task = RunAll();
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(_ => app.Dispatcher.InvokeAsync(() => frame.Continue = false), TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        try { task.GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); Console.WriteLine("Fixtures: " + Root); }
    }

    private static int Worker()
    {
        byte[] memory = new byte[96 * 1024 * 1024]; Array.Fill(memory, (byte)42);
        using var done = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var cpu = Task.Run(() => { while (!done.IsCancellationRequested) for (int i = 0; i < memory.Length; i += 4096) memory[i]++; });
        Console.WriteLine("Done (0.1s)! For help, type help");
        Console.WriteLine("[Server thread/INFO]: Alice[/127.0.0.1:123] logged in with entity id 1");
        string? command;
        while ((command = Console.ReadLine()) != null)
        {
            if (command == "stop") { done.Cancel(); cpu.Wait(); return 0; }
            if (command == "crash") { done.Cancel(); cpu.Wait(); return 7; }
            Console.WriteLine("ACK " + command);
        }
        done.Cancel(); cpu.Wait(); return 0;
    }

    private static ServerProfile Fixture(bool detached = false)
    {
        string folder = Path.Combine(Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string dll = Assembly.GetExecutingAssembly().Location;
        string command = $"dotnet \"{dll}\" --server";
        File.WriteAllText(Path.Combine(folder, "start.bat"), "@echo off\r\n" + (detached ? "start \"\" /b " : "") + command + "\r\n");
        File.WriteAllText(Path.Combine(folder, "eula.txt"), "eula=true\n");
        return new ServerProfile { DisplayName = "Test", FolderPath = folder, LaunchMode = "Batch", BatchFilename = "start.bat" };
    }
    private static ServerSession Session(ServerProfile profile, TimeProvider? clock = null) => new(profile, () => Task.FromResult(false), timeProvider: clock)
    { RetryDelay = TimeSpan.FromMilliseconds(200), StopTimeout = TimeSpan.FromSeconds(2) };
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Until(Func<bool> condition, string message)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(12)) throw new Exception("Timeout: " + message); await Task.Delay(30); }
    }
    private static async Task Test(string name, Func<Task> test) { await test(); Console.WriteLine("PASS " + name); }

    private static async Task RunAll()
    {
        await Test("refused startup restores the actual toggle binding", async () =>
        {
            var profile = Fixture(); File.WriteAllText(Path.Combine(profile.FolderPath, "eula.txt"), "eula=false");
            await using var session = Session(profile);
            using var model = new ServerMonitorViewModel(session);
            var view = new ServerMonitor { DataContext = model };
            view.Measure(new Size(1000, 680)); view.Arrange(new Rect(0, 0, 1000, 680)); view.UpdateLayout();
            try
            {
                var toggle = FindVisualChild<System.Windows.Controls.Primitives.ToggleButton>(view);
                // ToggleButton changes its local value before executing the bound command.
                toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                model.ToggleServerCommand.Execute(null);
                await Until(() => model.ToggleServerCommand.CanExecute(null), "refused startup completion");
                await Task.Delay(400);
                Assert(!model.IsRunning && toggle.IsChecked == false, "Refused startup leaves the switch enabled");
            }
            finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        });
        await Test("pending send after navigation preserves newest history and draft", async () =>
        {
            await using var session = Session(Fixture());
            await session.StartAsync();
            await Until(() => session.Snapshot.State == ServerState.Running, "pending history test startup");
            using var first = new ServerMonitorViewModel(session);
            var gate = (SemaphoreSlim)typeof(ServerSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            await gate.WaitAsync();
            ServerMonitorViewModel second;
            try
            {
                first.CommandInput = "say pending"; first.SendCommand.Execute(null);
                first.Dispose();
                second = new ServerMonitorViewModel(session);
            }
            finally { gate.Release(); }
            using (second)
            {
                await Until(() => first.CommandInput == "", "pending send completion");
                second.CommandInput = "new draft";
                second.NavigateHistoryCommand.Execute("Up");
                Assert(second.CommandInput == "say pending", "Late send skipped newest command");
                second.NavigateHistoryCommand.Execute("Down");
                Assert(second.CommandInput == "new draft", "Late send lost the new view's draft");
            }
        });
        await Test("idle monitor avoids broad notifications and still refreshes configuration and language", async () =>
        {
            var profile = new ServerProfile { DisplayName = "Before", JvmArguments = "-Xmx1G" };
            await using var session = Session(profile);
            using var model = new ServerMonitorViewModel(session);
            var changes = new List<string?>();
            model.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            await Task.Delay(800);
            Assert(changes.Count == 0, "Idle monitor repeatedly invalidates bindings");
            model.CommandInput = "";
            Assert(changes.Count == 0, "Unchanged command input invalidates bindings");

            profile.JvmArguments = "-Xmx512m -Xmx2G";
            await Until(() => changes.Contains(nameof(model.MaxRamMb)), "heap limit notification");
            Assert(model.MaxRamMb == 2048 && model.HasHeapLimit, "Heap limit cache stayed stale");
            changes.Clear(); profile.LaunchMode = "Batch";
            await Until(() => changes.Contains(nameof(model.HasHeapLimit)), "batch heap limit notification");
            Assert(model.MaxRamMb == 0 && !model.HasHeapLimit, "Batch launch retained a Java heap limit");

            changes.Clear(); profile.DisplayName = "After"; profile.IconBase64 = null;
            await Until(() => changes.Contains(nameof(model.DisplayName)) && changes.Contains(nameof(model.ServerIconSource)), "profile display notifications");
            changes.Clear(); string oldStatus = model.ServerStatusText, oldDetail = model.RamDetailText;
            try
            {
                ThemeResources.ApplyLanguage("fr-FR");
                await Until(() => changes.Contains(nameof(model.ServerStatusText)) && changes.Contains(nameof(model.RamDetailText)), "localized monitor notifications");
                Assert(model.ServerStatusText != oldStatus && model.RamDetailText != oldDetail, "Localized monitor text stayed stale");
                Assert(!changes.Contains(null), "Monitor still invalidates all properties");
            }
            finally { ThemeResources.ApplyLanguage("en-US"); }
            model.Dispose(); changes.Clear(); profile.DisplayName = "Disposed";
            await Task.Delay(400);
            Assert(changes.Count == 0, "Disposed monitor still publishes changes");
        });
        await Test("running monitor publishes metrics and players without refreshing unrelated bindings", async () =>
        {
            await using var session = Session(Fixture());
            await session.StartAsync();
            await Until(() => session.Snapshot.State == ServerState.Running, "monitor test startup");
            using var model = new ServerMonitorViewModel(session);
            Assert(model.PlayerCount == 1, "New monitor did not load existing players");
            var changes = new List<string?>(); model.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            session.AddLog("[Server thread/INFO]: Bob joined the game");
            await Until(() => model.PlayerCount == 2 && changes.Contains(nameof(model.UptimeText)), "player and uptime notifications");
            Assert(changes.Contains(nameof(model.PlayerCount)), "Player count binding not notified");
            Assert(!changes.Contains(null) && !changes.Contains(nameof(model.CommandInput)) && !changes.Contains(nameof(model.DisplayName)),
                "Metrics refresh invalidates unrelated bindings");
            model.CommandInput = "say available";
            Assert(model.SendCommand.CanExecute(null), "Running server cannot send commands");
            await session.StopAsync();
            await Until(() => !model.IsRunning && model.PlayerCount == 0, "stopped monitor state");
            Assert(!model.SendCommand.CanExecute(null) && changes.Contains(nameof(model.IsRunning)), "Stopped monitor command/state is stale");
        });
        await Test("command editing preserves the caret and history restores the draft", async () =>
        {
            await using var session = Session(Fixture());
            await session.StartAsync();
            await Until(() => session.Snapshot.State == ServerState.Running, "command test startup");
            using var model = new ServerMonitorViewModel(session);
            var view = new ServerMonitor { DataContext = model };
            view.Measure(new Size(1000, 680)); view.Arrange(new Rect(0, 0, 1000, 680)); view.UpdateLayout();
            try
            {
                var box = FindVisualChild<TextBox>(view);
                model.CommandInput = "say hello";
                box.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
                box.CaretIndex = 4;
                box.SelectedText = "new ";
                Assert(box.Text == "say new hello" && box.CaretIndex < box.Text.Length,
                    "TextChanged moves the caret away from the edit position");

                foreach (var command in new[] { "say first", "say second" })
                {
                    model.CommandInput = command; model.SendCommand.Execute(null);
                    await Until(() => model.CommandInput == "", "command sent");
                }
                model.CommandInput = "say unfinished";
                using var inputSource = new System.Windows.Interop.HwndSource(
                    new System.Windows.Interop.HwndSourceParameters("MSM command test") { Width = 1, Height = 1, WindowStyle = 0 });
                foreach (var (key, expected) in new[]
                {
                    (Key.Up, "say second"), (Key.Up, "say first"),
                    (Key.Down, "say second"), (Key.Down, "say unfinished")
                })
                {
                    var input = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                    box.RaiseEvent(input);
                    Assert(input.Handled && model.CommandInput == expected && box.Text == expected, "History navigation lost the command or draft");
                    Assert(box.CaretIndex == box.Text.Length, "History recall did not move the caret to the end");
                }
            }
            finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        });
        await Test("command history survives navigation and is isolated per server", async () =>
        {
            await using var session = Session(Fixture());
            await session.StartAsync();
            await Until(() => session.Snapshot.State == ServerState.Running, "history test startup");
            using (var first = new ServerMonitorViewModel(session))
            {
                for (int i = 0; i <= 100; i++)
                {
                    first.CommandInput = "say " + i; first.SendCommand.Execute(null);
                    await Until(() => first.CommandInput == "", "remembered command sent");
                }
            }
            using var second = new ServerMonitorViewModel(session);
            second.NavigateHistoryCommand.Execute("Up");
            Assert(second.CommandInput == "say 100", "Navigation lost command history");
            for (int i = 0; i < 105; i++) second.NavigateHistoryCommand.Execute("Up");
            Assert(second.CommandInput == "say 1", "History did not retain exactly the last 100 commands");
            await using var otherSession = Session(Fixture());
            using var other = new ServerMonitorViewModel(otherSession);
            other.CommandInput = "other draft"; other.NavigateHistoryCommand.Execute("Up");
            Assert(other.CommandInput == "other draft", "History leaked between servers or erased an empty-history draft");
        });
        await Test("player events reject chat quotes and accept vanilla, Forge and Paper prefixes", async () =>
        {
            await using var session = Session(Fixture());
            await session.StartAsync();
            await Until(() => session.Snapshot.Players.SequenceEqual(new[] { "Alice" }), "initial login");
            foreach (var line in new[]
            {
                "[Server thread/INFO]: <Bob> example: Alice lost connection: Disconnected",
                "[Server thread/INFO]: <Bob> example: Alice left the game",
                "[Server thread/INFO]: [Notices] example: Eve joined the game",
                "[Server thread/INFO]: <Bob> example: Eve[/127.0.0.1:123] logged in with entity id 2",
                "[Server thread/INFO]: Eve joined the game yesterday",
                "[Server thread/INFO]: Alice left the game yesterday"
            }) session.AddLog(line);
            var previous = session.Snapshot;
            await Until(() => !ReferenceEquals(previous, session.Snapshot), "snapshot after chat quotes");
            Assert(session.Snapshot.Players.SequenceEqual(new[] { "Alice" }), "Chat quotes changed connected players");

            foreach (var prefix in new[]
            {
                "[12:34:56] [Server thread/INFO]: ",
                "[12:34:56] [Server thread/INFO] [minecraft/PlayerList]: ",
                "[12:34:56 INFO]: "
            })
            {
                session.AddLog(prefix + "Bob_2[/127.0.0.1:123] logged in with entity id 2");
                session.AddLog(prefix + "Bob_2 joined the game");
                await Until(() => session.Snapshot.Players.SequenceEqual(new[] { "Alice", "Bob_2" }), "login prefix " + prefix);
                session.AddLog(prefix + "bob_2 lost connection: Timed out");
                await Until(() => session.Snapshot.Players.SequenceEqual(new[] { "Alice" }), "logout prefix " + prefix);
            }
        });
        await Test("disconnect tracking ignores decorative titles and duplicate leave messages", async () =>
        {
            var profile = Fixture(); await using var session = Session(profile);
            await session.StartAsync();
            await Until(() => session.Snapshot.Players.SequenceEqual(new[] { "Alice" }), "initial player login");
            session.AddLog("[Server thread/INFO]: Alice joined the game");
            session.AddLog("[Server thread/INFO]: Bob joined the game");
            session.AddLog("[Server thread/INFO]: Alice1 joined the game");
            await Until(() => session.Snapshot.Players.Length == 3, "distinct players, without duplicate login");

            foreach (var (displayName, reason) in new[]
            {
                ("[Champion] Alice", "Disconnected"),
                ("Alice the Brave", "Timed out"),
                ("\u00a76[Champion] \u00a7bAlice\u00a7r", "Internal Exception: java.io.IOException"),
                ("Alice", "Disconnected")
            })
            {
                session.AddLog("[Server thread/INFO]: Alice[/127.0.0.1:123] logged in with entity id 1");
                await Until(() => session.Snapshot.Players.Length == 3, "player reconnect");
                session.AddLog($"[Server thread/INFO]: Alice lost connection: {reason}");
                session.AddLog($"[Server thread/INFO]: {displayName} left the game");
                await Until(() => session.Snapshot.Players.SequenceEqual(new[] { "Alice1", "Bob" }),
                    "disconnect with display name " + displayName);
                Assert(profile.PlayerCount == 2, "Profile player count not updated after disconnect");
            }

            session.AddLog("[Server thread/INFO]: Bob left the game");
            await Until(() => session.Snapshot.Players.SequenceEqual(new[] { "Alice1" }), "vanilla leave without disconnect line");
        });
        await Test("batch CPU/RAM, navigation, players, serialized commands and stop", async () =>
        {
            var profile = Fixture(); await using var session = Session(profile);
            var lines = new ConcurrentQueue<string>(); session.LogReceived += lines.Enqueue;
            await session.StartAsync();
            await Until(() => session.Snapshot.Metrics.CpuPercent is > 0 && session.Snapshot.Metrics.ResidentMegabytes is > 80, "workload metrics");
            Console.WriteLine($"MEASURED batch: {session.Snapshot.Metrics.ResidentMegabytes:F0} MiB, {session.Snapshot.Metrics.CpuPercent:F1}% CPU, {session.Snapshot.Metrics.ProcessCount} processes");
            using (var first = new ServerMonitorViewModel(session)) Assert(first.PlayerCount == 1, "Initial players missing");
            await Task.Delay(1100);
            using (var second = new ServerMonitorViewModel(session))
            {
                Assert(second.PlayerCount == 1, "Navigation lost players");
                Assert(second.CpuUsageText != "—", "Navigation lost metrics");
                CaptureMonitor(second);
            }
            await Task.WhenAll(Enumerable.Range(0, 100).Select(i => session.SendCommandAsync("test-" + i)));
            await Until(() => lines.Count(l => l.StartsWith("ACK test-")) == 100, "commands missing/interleaved");
            await session.StopAsync();
            await Task.Delay(500);
            Assert(session.Snapshot.State == ServerState.Stopped, "Manual stop restarted server");
            Assert(!profile.IsRunning && profile.PlayerCount == 0, "Profile not reset");
        });
        await Test("direct launch CPU/RAM and lowercase heap argument", async () =>
        {
            var profile = Fixture();
            profile.LaunchMode = "Java"; profile.JdkPath = "dotnet";
            profile.JvmArguments = $"\"{Assembly.GetExecutingAssembly().Location}\" --server -Xmx128m";
            File.WriteAllText(Path.Combine(profile.FolderPath, "server.jar"), "test launch placeholder");
            await using var session = Session(profile); await session.StartAsync();
            await Until(() => session.Snapshot.Metrics.ResidentMegabytes > 80 && session.Snapshot.Metrics.CpuPercent > 0, "direct metrics");
            using var view = new ServerMonitorViewModel(session);
            Assert(view.MaxRamMb == 128, "Lowercase heap suffix ignored");
            Console.WriteLine($"MEASURED direct: {view.RamUsageText}, {view.CpuUsageText}");
        });
        await Test("detached batch child is measured and stopped", async () =>
        {
            await using var session = Session(Fixture(detached: true));
            await session.StartAsync();
            await Until(() => session.Snapshot.Metrics.ResidentMegabytes is > 80 && session.Snapshot.Metrics.CpuPercent is > 0, "detached child metrics");
            await session.StopAsync();
            Assert(session.Snapshot.State == ServerState.Stopped, "Detached child not stopped");
        });
        await Test("crash retry and shutdown cancellation", async () =>
        {
            await using var session = Session(Fixture());
            int starts = 0; session.LogReceived += line => { if (line.Contains("Done (")) Interlocked.Increment(ref starts); };
            await session.StartAsync(); await Until(() => session.Snapshot.State == ServerState.Running, "initial start");
            await session.SendCommandAsync("crash");
            await Until(() => starts == 2 && session.Snapshot.State == ServerState.Running, "crash retry");
            await session.SendCommandAsync("crash"); await session.StopAsync();
            int stoppedCount = starts; await Task.Delay(700);
            Assert(starts == stoppedCount && session.Snapshot.State == ServerState.Stopped, "Retry ignored stop");
            await session.DisposeAsync(); await session.DisposeAsync();
        });
        await Test("scheduled restart independent of navigation", async () =>
        {
            var profile = Fixture(); profile.AutoRestartEnabled = true; profile.AutoRestartTime = "04:00";
            await using var session = Session(profile, new FixedClock());
            var commands = new ConcurrentQueue<string>(); session.LogReceived += commands.Enqueue;
            await session.StartAsync();
            await session.StartAsync(); // Idempotent: must not invalidate background scheduling.
            await Until(() => commands.Any(l => l.StartsWith("ACK say ")), "background restart schedule");
            await session.StopAsync(); // Cancels the ten-second warning without relaunching.
            Assert(session.Snapshot.State == ServerState.Stopped, "Scheduled delay could not be cancelled");
        });
        await Test("EULA refusal, failed launch and JSON runtime isolation", async () =>
        {
            var profile = Fixture(); File.WriteAllText(Path.Combine(profile.FolderPath, "eula.txt"), "eula=false");
            await using var session = Session(profile); await session.StartAsync();
            Assert(session.Snapshot.State == ServerState.Stopped && !profile.IsRunning, "EULA refusal left server running");
            profile.BatchFilename = "missing.bat";
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try { await session.StartAsync(); throw new Exception("Expected missing file"); }
                catch (FileNotFoundException) { }
            }
            profile.IsRunning = true;
            var json = JsonSerializer.Serialize(profile);
            Assert(!json.Contains("IsRunning") && !json.Contains("ServerProcess"), "Runtime state persisted");
        });
        await Test("backup failure, parent named Backups, content and retention", async () =>
        {
            string folder = Path.Combine(Root, "Backups", "server"); Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "level.dat"); File.WriteAllText(file, "synthetic-world");
            string backups = Path.Combine(folder, "Backups"); Directory.CreateDirectory(backups);
            string old = Path.Combine(backups, "Backup_old.zip"); File.WriteAllText(old, "last copy"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-10));
            using (var locked = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                try { await ServerBackup.CreateAsync(folder); throw new Exception("Unreadable file reported as success"); }
                catch (IOException) { }
                Assert(File.Exists(old), "Failed backup purged previous copy");
                Assert(!Directory.GetFiles(backups, "*.partial").Any(), "Partial backup not removed");
            }
            string result = await ServerBackup.CreateAsync(folder);
            using var archive = ZipFile.OpenRead(result);
            using var reader = new StreamReader(archive.GetEntry("level.dat")!.Open());
            Assert(await reader.ReadToEndAsync() == "synthetic-world", "Backup content incorrect");
            Assert(!File.Exists(old), "Retention not applied after success");
        });
        await Test("running backup stops and restarts the same session", async () =>
        {
            await using var session = Session(Fixture()); await session.StartAsync();
            await Until(() => session.Snapshot.State == ServerState.Running, "initial run");
            var path = await session.BackupAsync(); Assert(File.Exists(path), "Backup missing");
            await Until(() => session.Snapshot.State == ServerState.Running && !session.Snapshot.IsBackingUp, "resume after backup");
        });
        await Test("properties preserve comments, whitespace and external edits", () =>
        {
            string folder = Fixture().FolderPath; string path = Path.Combine(folder, "server.properties");
            File.WriteAllText(path, "# comment\n! another comment\nmotd=hello=world  \nmax-players=20\n");
            var config = new ServerConfiguration(folder); var items = config.Read(); items.Single(i => i.Key == "max-players").Value = "30";
            config.Save(items); Assert(File.ReadAllText(path).Contains("motd=hello=world  \n"), "Unchanged property changed");
            File.AppendAllText(path, "online-mode=true\n");
            try { config.Save(items); throw new Exception("External edit overwritten"); } catch (IOException) { }
            return Task.CompletedTask;
        });
        await Test("archive traversal, quota, invalid pack and script detection", async () =>
        {
            string folder = Fixture().FolderPath;
            foreach (var scenario in new[] { "../escape", "readme.txt", "huge" })
            {
                string zip = Path.Combine(folder, Guid.NewGuid() + ".zip");
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry(scenario).Open())) writer.Write("12345");
                try { await ServerInstaller.InstallAsync(zip, folder, 4, default, scenario == "huge" ? 1 : 100); throw new Exception("Invalid archive accepted"); }
                catch (InvalidDataException) { }
            }
            string valid = Path.Combine(folder, "Pack.zip");
            using (var archive = ZipFile.Open(valid, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("pack/run.bat").Open())) writer.Write("java -version");
            var profile = await ServerInstaller.InstallAsync(valid, folder, 4, default);
            Assert(profile.LaunchMode == "Batch" && File.Exists(Path.Combine(profile.FolderPath, "run.bat")), "Script pack not detected");
            Assert(!ServerInstaller.IsTrustedUri("https://example.org/download/minecraft/modpacks"), "Untrusted origin accepted");
            Assert(!Directory.GetDirectories(folder, ".msm-install-*").Any(), "Staging directories leaked");
        });
        await Test("console batching and language dictionary lifetime", () =>
        {
            var view = new ServerMonitor();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var queue = (ConcurrentQueue<string>)typeof(ServerMonitor).GetField("_logQueue", flags)!.GetValue(view)!;
            for (int i = 0; i < 101; i++) queue.Enqueue("line " + i);
            typeof(ServerMonitor).GetMethod("ProcessLogQueue", flags)!.Invoke(view, [null, EventArgs.Empty]);
            var box = (RichTextBox)view.FindName("ConsoleOutput");
            Assert(box.Document.Blocks.Count == 100 && queue.Count == 1, "Batch loses a line");
            ((DispatcherTimer)typeof(ServerMonitor).GetField("_logUpdateTimer", flags)!.GetValue(view)!).Stop();
            for (int i = 0; i < 10; i++) ThemeResources.ApplyLanguage(i % 2 == 0 ? "fr-FR" : "en-US");
            Assert(Application.Current.Resources.MergedDictionaries.Count == 1, "Language resources accumulate");
            return Task.CompletedTask;
        });
        await Test("catalogue validation, draft isolation and permission deltas", async () =>
        {
            var profile = Fixture(); var catalog = new ServerCatalog(Path.Combine(profile.FolderPath, "profiles"));
            catalog.Save(profile);
            var draft = ServerCatalog.Clone(profile); draft.DisplayName = "Changed";
            Assert(profile.DisplayName == "Test", "Draft changed the running definition");
            catalog.Save(draft); Assert(catalog.Load(_ => throw new Exception("Invalid catalogue")).Single().DisplayName == "Changed", "Catalogue save failed");
            profile.Id = "../escape";
            try { catalog.Save(profile); throw new Exception("Invalid profile ID accepted"); } catch (InvalidDataException) { }
            profile.Id = Guid.NewGuid().ToString();
            await using var session = Session(profile);
            await session.ChangePermissionAsync(new WhitelistPlayer { Name = "Alice", Uuid = "a" }, false, false);
            await session.ChangePermissionAsync(new WhitelistPlayer { Name = "Bob", Uuid = "b" }, false, false);
            await session.ChangePermissionAsync(new WhitelistPlayer { Name = "Alice", Uuid = "a" }, false, true);
            var list = JsonSerializer.Deserialize<List<WhitelistPlayer>>(File.ReadAllText(Path.Combine(profile.FolderPath, "whitelist.json")))!;
            Assert(list.Count == 1 && list[0].Name == "Bob", "Permission delta lost another player");
        });
        await Test("G1 preset preserves memory, custom arguments and diagnostic flags", () =>
        {
            string result = JavaArguments.ApplyG1Preset("-Xmx2G -Xms512M -Dcustom=enabled -XX:+UseZGC -XX:HeapDumpPath=custom.hprof", 8);
            Assert(result.Contains("-Xmx2G") && result.Contains("-Xms512M") && !result.Contains("-Xmx8G"), "Preset replaced memory budget");
            Assert(result.Contains("-Dcustom=enabled") && result.Contains("-XX:HeapDumpPath=custom.hprof"), "Preset lost custom options");
            Assert(!result.Contains("UseZGC") && result.Contains("UseG1GC"), "Conflicting GC flags");
            return Task.CompletedTask;
        });
        await Test("multi-server registry shutdown and active profile removal", async () =>
        {
            var registry = new ServerRegistry(() => Task.FromResult(false));
            var profile = Fixture(); var first = registry.Get(profile); var second = registry.Get(Fixture());
            try
            {
                await Task.WhenAll(first.StartAsync(), second.StartAsync());
                await Until(() => first.Snapshot.State == ServerState.Running && second.Snapshot.State == ServerState.Running, "two servers");
                await registry.RemoveAsync(profile);
                Assert(first.Snapshot.State == ServerState.Stopped && second.Snapshot.State == ServerState.Running, "Removal stopped the wrong server");
            }
            finally { await registry.ShutdownAsync(); }
            await registry.ShutdownAsync();
            Assert(second.Snapshot.State == ServerState.Stopped, "Global shutdown missed a server");
        });
        await Test("stop cancels a pending EULA startup", async () =>
        {
            var profile = Fixture(); File.WriteAllText(Path.Combine(profile.FolderPath, "eula.txt"), "eula=false");
            var agreement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var session = new ServerSession(profile, () => agreement.Task);
            var start = session.StartAsync();
            await Until(() => session.Snapshot.State == ServerState.Starting, "pending agreement");
            var stop = session.StopAsync(); agreement.SetResult(true);
            try { await start; throw new Exception("Startup was not cancelled"); } catch (OperationCanceledException) { }
            await stop;
            Assert(session.Snapshot.State == ServerState.Stopped && !profile.IsRunning, "Cancelled startup is running");
        });
    }

    private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindVisualChild<T>(child) is { } descendant) return descendant;
        }
        return null!;
    }

    private static void CaptureMonitor(ServerMonitorViewModel model)
    {
        var view = new ServerMonitor { DataContext = model, Width = 1000, Height = 680 };
        view.Measure(new Size(1000, 680)); view.Arrange(new Rect(0, 0, 1000, 680)); view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1000, 680, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(Root, "monitor-preview.png")); encoder.Save(file);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 10, 4, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
