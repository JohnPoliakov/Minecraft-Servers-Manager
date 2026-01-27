
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
* **Live Metrics:** Visualize **CPU and RAM usage** in real-time with beautiful charts (LiveCharts).
* **Console Output:** Full access to the server console log with color-coded output.
* **Command Input:** Send commands to the server with command history navigation (Up/Down arrows).
* **Player List:** See who is online in real-time.

### ⚙️ Advanced Configuration
* **GUI Editor:** Edit `server.properties`, Whitelist, and Ops without touching text files.
* **Java Management:** Automatically detects installed Java versions and allows specific Java paths per server.
* **Optimization:** One-click button to apply **Aikar's Flags** for optimal performance.
* **Scripting:** Allow you to run your own .bat script to start the server.
* **Backups:** Built-in backup manager (Create/Delete/Auto-cleanup old backups).
* **Auto-Restart:** Schedule automatic restarts at specific times.
* **Discord Webhooks:** Get notifications on Discord when the server starts, stops, or crashes.

### 🌍 Internationalization & Theming
* **Multi-language Support:** Fully translated into **English, French, Spanish, and German**.
* **Theming:** Choose from multiple color themes (Dark Blue, Midnight Black, Forest Green, etc.).
* **EULA Compliance:** User-friendly prompt to accept Mojang's EULA safely.

---

## 🚀 Getting Started

### Prerequisites
* **OS:** Windows 10 or 11.
* **.NET Runtime:** .NET 6.0 or higher.
* **WebView2 Runtime:** (Usually pre-installed on modern Windows).

---

## 🛠️ Building from Source

If you want to contribute or modify the code:

1.  Clone the repository:
    ```bash
    git clone [https://github.com/YourUsername/Minecraft-Server-Manager.git](https://github.com/YourUsername/Minecraft-Server-Manager.git)
    ```
2.  Open the solution in **Visual Studio 2022**.
3.  Ensure you have the **.NET Desktop Development** workload installed.
4.  Restore NuGet packages.
5.  Build the solution (`Ctrl+Shift+B`).

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
