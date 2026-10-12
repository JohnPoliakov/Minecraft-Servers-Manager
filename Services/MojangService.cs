using Minecraft_Server_Manager.Models;
using System.Net.Http;
using System.Text.Json;

namespace Minecraft_Server_Manager.Services
{
    public static class MojangService
    {
        private static readonly HttpClient _client = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<WhitelistPlayer?> GetPlayerProfileAsync(string username)
        {
            try
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[A-Za-z0-9_]{1,16}$")) return null;
                string url = $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(username)}";

                using var response = await _client.GetAsync(url);

                if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.NoContent) return null;
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var profile = JsonSerializer.Deserialize<MojangProfile>(json);

                if (profile == null) return null;

                if (!Guid.TryParseExact(profile.Id, "N", out var uuid)) throw new JsonException("Invalid player UUID.");
                string formattedUuid = uuid.ToString("D");

                return new WhitelistPlayer { Name = profile.Name, Uuid = formattedUuid };
            }
            catch (OperationCanceledException ex) { throw new HttpRequestException("La recherche du joueur a dépassé le délai autorisé.", ex); }
        }
    }
}
