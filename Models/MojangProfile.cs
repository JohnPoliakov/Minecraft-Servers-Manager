using System.Text.Json.Serialization;

namespace Minecraft_Server_Manager.Models
{
    public class MojangProfile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
}
