using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Wanderlust.Services;

public class HotelbedsService(HttpClient client, IConfiguration config)
{
    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var apiKey = config["HOTELBEDS_API_KEY"]?.Trim()
            ?? throw new InvalidOperationException("Hotelbeds API key missing.");

        var secret = config["HOTELBEDS_API_SECRET"]?.Trim()
            ?? throw new InvalidOperationException("Hotelbeds API secret missing.");

        var timestamp = DateTimeOffset.UtcNow
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var input = Encoding.UTF8.GetBytes(apiKey + secret + timestamp);
        var signature = Convert.ToHexString(
            SHA256.HashData(input)).ToLowerInvariant();

        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Api-key", apiKey);
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("Accept", "application/json");

        return request;
    }

    public async Task<(int StatusCode, string Body)> CheckStatusAsync()
    {
        using var request = CreateRequest(
            HttpMethod.Get, "hotel-api/1.0/status");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return ((int)response.StatusCode, body);
    }

    public async Task<(int StatusCode, string Body)> SearchHotelsAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        int adults)
    {
        var payload = new
        {
            stay = new
            {
                checkIn = checkIn.ToString("yyyy-MM-dd"),
                checkOut = checkOut.ToString("yyyy-MM-dd")
            },
            occupancies = new[]
            {
                new
                {
                    rooms = 1,
                    adults,
                    children = 0
                }
            },
            geolocation = new
            {
                latitude = 40.7580,
                longitude = -73.9855,
                radius = 20,
                unit = "km"
            }
        };

        using var request = CreateRequest(
            HttpMethod.Post, "hotel-api/1.0/hotels");

        request.Content = JsonContent.Create(payload);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return ((int)response.StatusCode, body);
    }
}
