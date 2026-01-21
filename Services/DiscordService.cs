using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Minecraft_Server_Manager.Services
{
    public static class DiscordService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static int ColorGreen = 5763719;
        public static int ColorRed = 15548997;
        public static int ColorOrange = 16776960;
        public static int ColorBlue = 3447003;

        public static async Task SendNotification(string webhookUrl, string title, string description, int color)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl)) return;

            try
            {
                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = title,
                            description = description,
                            color = color,
                            footer = new { text = $"Manager - {DateTime.Now:HH:mm:ss}" }
                        }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync(webhookUrl, content);
            }
            catch
            {
            }
        }
    }
}