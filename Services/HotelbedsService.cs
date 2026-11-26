using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Wanderlust.Services;

public class HotelbedsService(
    HttpClient client,
    IConfiguration config,
    IHttpClientFactory httpClientFactory)
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
        string destination,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults)
    {
        var geocoder = httpClientFactory.CreateClient("Geocoding");
        var geocodeUrl =
            "https://geocoding-api.open-meteo.com/v1/search?name=" +
            Uri.EscapeDataString(destination) +
            "&count=1&language=en&format=json";

        using var geoResponse = await geocoder.GetAsync(geocodeUrl);
        if (!geoResponse.IsSuccessStatusCode)
        {
            return (502, JsonSerializer.Serialize(new
            {
                error = "Could not look up that destination. Please try another city."
            }));
        }

        using var geoJson = JsonDocument.Parse(
            await geoResponse.Content.ReadAsStringAsync());

        if (!geoJson.RootElement.TryGetProperty("results", out var results) ||
            results.GetArrayLength() == 0)
        {
            return (404, JsonSerializer.Serialize(new
            {
                error = $"We couldn't find the destination '{destination}'. Try a city name."
            }));
        }

        var place = results[0];
        var latitude = place.GetProperty("latitude").GetDouble();
        var longitude = place.GetProperty("longitude").GetDouble();

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
                latitude,
                longitude,
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
