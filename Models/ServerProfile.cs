using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace Minecraft_Server_Manager.Models
{
    public class ServerProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DisplayName { get; set; }
        public string FolderPath { get; set; }
        public string JdkPath { get; set; }
        public string JvmArguments { get; set; }
        public string JarName { get; set; } = "server.jar";
        public string IconBase64 { get; set; }
        public bool IsRunning { get; set; }

        [JsonIgnore]
        public StringBuilder CachedLogs { get; } = new StringBuilder();

        [JsonIgnore]
        public bool IsBusy { get; set; }

        [JsonIgnore]
        public Process ServerProcess { get; set; }

        public event Action<string> LogReceived;

        public void AddLog(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            CachedLogs.AppendLine(data);
            LogReceived?.Invoke(data);
        }
    }
}