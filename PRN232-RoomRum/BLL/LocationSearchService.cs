using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace RoomRum.BLL;

public sealed record LocationSearchResponse(string DisplayName, double Latitude, double Longitude);

public interface ILocationSearchService
{
    Task<ApiOutcome> Search(string? query, CancellationToken cancellationToken);
}

public sealed class LocationSearchService(IHttpClientFactory clients, IConfiguration configuration) : ILocationSearchService
{
    private static readonly ConcurrentDictionary<string, (LocationSearchResponse? Result, DateTimeOffset Expires)> Cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    public async Task<ApiOutcome> Search(string? query, CancellationToken cancellationToken)
    {
        var term = query?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length > 200)
            return ApiOutcome.BadRequest("Nhập địa điểm dài từ 1 đến 200 ký tự.");
        if (Cache.TryGetValue(term, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
            return cached.Result is null ? ApiOutcome.NotFound("Không tìm thấy địa điểm.") : ApiOutcome.Ok(cached.Result);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(term, out cached) && cached.Expires > DateTimeOffset.UtcNow)
                return cached.Result is null ? ApiOutcome.NotFound("Không tìm thấy địa điểm.") : ApiOutcome.Ok(cached.Result);

            var wait = _lastRequest.AddSeconds(1.1) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
            var endpoint = configuration["Maps:GeocodingUrl"] ?? "https://nominatim.openstreetmap.org/search";
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
                return ApiOutcome.Unavailable("Dịch vụ bản đồ chưa được cấu hình hợp lệ.");
            var url = $"{endpoint}?format=jsonv2&limit=1&countrycodes=vn&q={Uri.EscapeDataString(term)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(configuration["Maps:UserAgent"] ?? "RoomRum/1.0 (+https://github.com/FostNg/PRN232-GroupProject)");
            _lastRequest = DateTimeOffset.UtcNow;
            using var response = await clients.CreateClient("maps").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm tạm thời không khả dụng.");
            var candidates = await response.Content.ReadFromJsonAsync<List<NominatimPlace>>(cancellationToken: cancellationToken);
            var place = candidates?.FirstOrDefault();
            LocationSearchResponse? result = null;
            if (place is not null && double.TryParse(place.Lat, System.Globalization.CultureInfo.InvariantCulture, out var latitude) &&
                double.TryParse(place.Lon, System.Globalization.CultureInfo.InvariantCulture, out var longitude) &&
                double.IsFinite(latitude) && double.IsFinite(longitude) && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
                result = new LocationSearchResponse(place.DisplayName ?? term, latitude, longitude);
            if (Cache.Count >= 500) Cache.Clear();
            Cache[term] = (result, result is null ? DateTimeOffset.UtcNow.AddHours(1) : DateTimeOffset.UtcNow.AddDays(7));
            return result is null ? ApiOutcome.NotFound("Không tìm thấy địa điểm.") : ApiOutcome.Ok(result);
        }
        catch (HttpRequestException)
        {
            return ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm tạm thời không khả dụng.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm phản hồi quá chậm.");
        }
        catch (System.Text.Json.JsonException)
        {
            return ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm trả dữ liệu không hợp lệ.");
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed class NominatimPlace
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
    }
}
