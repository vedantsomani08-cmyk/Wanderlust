using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wanderlust.Services
{
    /// <summary>
    /// Talks to the Anthropic (Claude) API on behalf of the browser.
    /// The API key stays on the server, so users never see it.
    /// </summary>
    public class ClaudeService
    {
        private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";

        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<ClaudeService> _logger;

        public ClaudeService(HttpClient http, IConfiguration config, ILogger<ClaudeService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Sends the request body to Anthropic and returns (status code, JSON text).
        /// </summary>
        public async Task<(int StatusCode, string Json)> SendMessageAsync(string requestBody)
        {
            // 1. Read settings from appsettings.json / environment variables
            string? apiKey = _config["Anthropic:ApiKey"];
            string? model = _config["Anthropic:Model"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (500, ErrorJson("Anthropic API key is missing. Add it to appsettings.json (Anthropic:ApiKey)."));
            }

            // 2. Parse the JSON from the browser
            JsonNode? payload;
            try
            {
                payload = JsonNode.Parse(requestBody);
            }
            catch (JsonException)
            {
                return (400, ErrorJson("Request body is not valid JSON."));
            }

            if (payload is not JsonObject obj)
            {
                return (400, ErrorJson("Request body must be a JSON object."));
            }

            // 3. Use the model from settings (so you can change it without touching the HTML)
            if (!string.IsNullOrWhiteSpace(model))
            {
                obj["model"] = model;
            }

            // 4. Build the HTTP request to Anthropic
            using var message = new HttpRequestMessage(HttpMethod.Post, AnthropicUrl);
            message.Headers.Add("x-api-key", apiKey);
            message.Headers.Add("anthropic-version", "2023-06-01");
            message.Content = new StringContent(obj.ToJsonString(), Encoding.UTF8, "application/json");

            // 5. Send it and pass the answer back to the browser unchanged
            try
            {
                using HttpResponseMessage response = await _http.SendAsync(message);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Anthropic returned {Status}: {Body}", (int)response.StatusCode, responseJson);
                }

                return ((int)response.StatusCode, responseJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not reach Anthropic API");
                return (502, ErrorJson("Could not reach the AI service: " + ex.Message));
            }
        }

        private static string ErrorJson(string text) =>
            JsonSerializer.Serialize(new { type = "error", error = new { message = text } });
    }
}
