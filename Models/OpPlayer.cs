using System.Text.Json.Serialization;

namespace Minecraft_Server_Manager.Models
{
    public class OpPlayer : WhitelistPlayer
    {
        [JsonPropertyName("level")]
        public int Level { get; set; } = 4;

        [JsonPropertyName("bypassesPlayerLimit")]
        public bool BypassesPlayerLimit { get; set; } = false;
    }
}
