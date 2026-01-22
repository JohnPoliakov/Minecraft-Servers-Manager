using Minecraft_Server_Manager.Models;
using System.Net.Http;
using System.Text.Json;

namespace Minecraft_Server_Manager.Services
{
    public static class MojangService
    {
        private static readonly HttpClient _client = new HttpClient();

        public static async Task<WhitelistPlayer> GetPlayerProfileAsync(string username)
        {
            try
            {
                string url = $"https://api.mojang.com/users/profiles/minecraft/{username}";

                var response = await _client.GetAsync(url);

                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                var profile = JsonSerializer.Deserialize<MojangProfile>(json);

                if (profile == null) return null;

                string raw = profile.Id;
                string formattedUuid = $"{raw.Substring(0, 8)}-{raw.Substring(8, 4)}-{raw.Substring(12, 4)}-{raw.Substring(16, 4)}-{raw.Substring(20)}";

                return new WhitelistPlayer { Name = profile.Name, Uuid = formattedUuid };
            }
            catch
            {
                return null;
            }
        }
    }
}