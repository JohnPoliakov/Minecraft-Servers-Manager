
# <img src="https://github.com/JohnPoliakov/Minecraft-Servers-Manager/blob/master/Resources/MSM.ico?raw=true" width="50" align="middle"/> <sub>Minecraft Servers Manager (MSM)</sub>

![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20WPF-lightgrey.svg)
![Status](https://img.shields.io/badge/status-Active-success.svg)

**Minecraft Server Manager** is a modern, lightweight, and powerful desktop application designed to simplify the creation, management, and monitoring of local Minecraft servers. Built with **C# and WPF**.

Whether you want to run a vanilla server for friends or a heavy modpack from CurseForge, MSM handles the heavy lifting.

---

## ✨ Key Features

### 🖥️ Dashboard & Management
* **Visual Interface:** Manage multiple servers from a clean, card-based dashboard.
* **One-Click Start/Stop:** Easily toggle servers online or offline with visual status indicators.
* **Server Import:** Detects existing server folders automatically.
* **System Tray Integration:** Minimizes to the system tray to keep servers running in the background without cluttering your taskbar.

### 📥 CurseForge Integration
* **Built-in Browser:** Integrated **WebView2** browser to navigate CurseForge directly within the app.
* **Auto-Installer:** Automatically detects Modpack downloads (`.zip`), extracts them, and sets up the server folder structure for you.

### 📊 Real-Time Monitoring
* **Live Metrics:** CPU and resident RAM for the server and its child processes, including batch launchers. CPU is a percentage of the entire machine; memory is shown in MiB, separately from the Java heap limit. A dash means a measurement is unavailable or warming up.
* **Console Output:** Full access to the server console log with color-coded output.
* **Command Input:** Send commands to the server with command history navigation (Up/Down arrows).
* **Player List:** See who is online in real-time.

### ⚙️ Advanced Configuration
* **GUI Editor:** Edit `server.properties`, Whitelist, and Ops without touching text files.
* **Java Management:** Automatically detects installed Java versions and allows specific Java paths per server.
* **Optimization:** Apply a G1 preset while preserving the memory budget and diagnostic options. Measure performance with your own Java version and pack.
* **Scripting:** Allow you to run your own .bat script to start the server.
* **Backups:** Create ZIP backups with retention after a successful backup. A running server is stopped cleanly, backed up, then restarted; the UI asks before this interruption. An unreadable file fails the backup. Restore by extracting a verified archive into a separate folder while the server is stopped; there is no automatic restore UI.
* **Auto-Restart:** Schedule automatic restarts at specific times.
* **Discord Webhooks:** Get notifications on Discord when the server starts, stops, or crashes.

### 🌍 Internationalization & Theming
* **Multi-language Support:** Interface resources in **English, French, Spanish, and German**. Some diagnostic messages remain in French.
* **Theming:** Choose from multiple color themes (Dark Blue, Midnight Black, Forest Green, etc.).
* **EULA Compliance:** User-friendly prompt to accept Mojang's EULA safely.

---

## 🚀 Getting Started

### Prerequisites
* **OS:** Windows 10 or 11.
* **.NET Runtime:** Included in the official Windows x64 distribution. Only framework-dependent builds require a separate .NET 10 Desktop Runtime, x64.
* **WebView2 Runtime:** (Usually pre-installed on modern Windows).

---

## 🛠️ Building from Source

If you want to contribute or modify the code:

1.  Clone the repository:
    ```bash
    git clone https://github.com/JohnPoliakov/Minecraft-Servers-Manager.git
    ```
2.  Install the **.NET 10 SDK** (see `global.json`) and open the solution in a compatible Visual Studio version, or use the CLI below.
3.  Ensure you have the **.NET Desktop Development** workload installed.
4.  Restore NuGet packages.
5.  Build the solution (`Ctrl+Shift+B`).

```powershell
dotnet restore "Minecraft Server Manager.csproj" --locked-mode
dotnet build "Minecraft Server Manager.csproj" -c Release --no-restore -warnaserror
dotnet run --project Tests/Msm.RegressionTests.csproj -c Release
dotnet publish "Minecraft Server Manager.csproj" -c Release --no-restore --self-contained false -o artifacts/msm
```

The stable release is `1.1.0`, displayed in the window title. To build its self-contained Windows x64 executable, use:

```powershell
dotnet publish "Minecraft Server Manager.csproj" -p:PublishProfile=Official-win-x64
```

The output is written to `artifacts/MSM-1.1.0-win-x64/`. WebView2 and Java remain separate prerequisites for their respective features. Keep all files included in a distributed archive together. Existing profile JSON files remain in Documents/Minecraft Servers Manager; runtime state is no longer persisted. Atomic writes retain a `.bak` file.

The regression runner uses isolated temporary folders and synthetic child processes, without contacting Minecraft, Mojang, CurseForge or Discord. It tests Java-style and batch launches, CPU/RAM, detached children, navigation, commands, shutdown/retry, backups, archive validation and WPF rendering. The Windows workflow runs the same checks.

`ServerRegistry` owns one `ServerSession` per server. Screens only subscribe to these sessions. `ProcessGroup` owns the Windows job and its metrics; `ServerBackup`, `ServerInstaller` and `ServerCatalog` own filesystem operations. See [implementation notes](docs/REFACTOR-IMPLEMENTATION.md) for behavior changes and verification limits.

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1.  Fork the project.
2.  Create your feature branch (`git checkout -b feature/AmazingFeature`).
3.  Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4.  Push to the branch (`git push origin feature/AmazingFeature`).
5.  Open a Pull Request.

---

## 📄 License

Distributed under the **Apache License 2.0**. See `LICENSE` for more information.

---

## ⚖️ Legal

This application is an unofficial tool and is not affiliated with, endorsed by, or associated with **Mojang Studios** or **Microsoft**.
"Minecraft" is a trademark of Mojang Synergies AB.
