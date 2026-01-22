using System.Text.Json.Serialization;

namespace Minecraft_Server_Manager.Models
{
    public class WhitelistPlayer
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
