
using System.Security.Cryptography;
using System.Text;

namespace Wanderlust.Services;

public class HotelbedsService(HttpClient client, IConfiguration config)
{
    public async Task<(int StatusCode, string Body)> CheckStatusAsync()
    {
        var apiKey = config["HOTELBEDS_API_KEY"]
            ?? throw new InvalidOperationException("Hotelbeds API key missing.");

        var secret = config["HOTELBEDS_API_SECRET"]
            ?? throw new InvalidOperationException("Hotelbeds API secret missing.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var input = Encoding.UTF8.GetBytes(apiKey + secret + timestamp);
        var signature = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "hotel-api/1.0/status");

        request.Headers.Add("Api-key", apiKey);
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("Accept", "application/json");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return ((int)response.StatusCode, body);
    }
}
