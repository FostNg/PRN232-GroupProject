using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class LocationSearchService(IHttpClientFactory clients, IConfiguration configuration) : ILocationSearchService
{
    private static readonly ConcurrentDictionary<string, (List<LocationSearchResponse> Results, DateTimeOffset Expires)> Cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    public async Task<ApiOutcome> Search(string? query, CancellationToken cancellationToken)
    {
        var term = query?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length > 200)
            return ApiOutcome.BadRequest("Nhập địa điểm dài từ 1 đến 200 ký tự.");
        var (results, error) = await Lookup(term, cancellationToken);
        if (error is not null) return error;
        var related = Related(results!, term);
        if (related.Count == 0) return ApiOutcome.NotFound("Không tìm thấy địa điểm phù hợp. Hãy thêm phường, quận hoặc thành phố.");
        var exact = related.Where(place => NormalizeName(place.DisplayName) == NormalizeName(term)).ToList();
        if (exact.Count == 1) return ApiOutcome.Ok(exact[0]);
        if (related.Count > 1)
            return ApiOutcome.Conflict("Có nhiều địa điểm phù hợp. Hãy dùng /api/locations/suggest và chọn một vị trí cụ thể.");
        return ApiOutcome.Ok(related[0]);
    }

    public async Task<ApiOutcome> Suggest(string? query, CancellationToken cancellationToken)
    {
        var term = query?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 3 || term.Length > 200)
            return ApiOutcome.BadRequest("Nhập ít nhất 3 ký tự và không quá 200 ký tự.");
        var (results, error) = await Lookup(term, cancellationToken);
        if (error is not null) return error;
        return ApiOutcome.Ok(Related(results!, term).Take(12).ToList());
    }

    private static List<LocationSearchResponse> Related(List<LocationSearchResponse> results, string term)
    {
        var subject = NormalizeName(term.Split(',', 2)[0].Trim());
        if (subject.Length == 0) return [];
        return results.Select(place => new
            {
                Place = place,
                Name = NormalizeName(place.DisplayName),
                FirstPart = NormalizeName(place.DisplayName.Split(',', 2)[0])
            })
            .Where(item =>
            {
                var index = item.Name.IndexOf(subject, StringComparison.Ordinal);
                return index >= 0 && (index + subject.Length >= item.Name.Length ||
                    !char.IsLetterOrDigit(item.Name[index + subject.Length]));
            })
            .OrderBy(item => item.FirstPart == subject ? 0 :
                item.FirstPart.StartsWith(subject, StringComparison.Ordinal) ? 1 :
                item.FirstPart.Contains(subject, StringComparison.Ordinal) ? 2 : 3)
            .ThenBy(item => item.FirstPart.Length)
            .Select(item => item.Place).ToList();
    }

    private static string NormalizeName(string value)
    {
        var text = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        }
        return result.ToString();
    }

    private async Task<(List<LocationSearchResponse>? Results, ApiOutcome? Error)> Lookup(
        string term, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(term, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
            return (cached.Results, null);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(term, out cached) && cached.Expires > DateTimeOffset.UtcNow)
                return (cached.Results, null);

            var wait = _lastRequest.AddSeconds(1.1) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
            var endpoint = configuration["Maps:GeocodingUrl"] ?? "https://photon.komoot.io/api/";
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
                return (null, ApiOutcome.Unavailable("Dịch vụ bản đồ chưa được cấu hình hợp lệ."));
            var url = $"{endpoint.TrimEnd('/')}?limit=50&countrycode=VN&q={Uri.EscapeDataString(term)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(configuration["Maps:UserAgent"] ?? "RoomRum/1.0 (+https://github.com/FostNg/PRN232-GroupProject)");
            _lastRequest = DateTimeOffset.UtcNow;
            using var response = await clients.CreateClient("maps").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return (null, ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm tạm thời không khả dụng."));
            var candidates = await response.Content.ReadFromJsonAsync<PhotonResponse>(cancellationToken: cancellationToken);
            var results = (candidates?.Features ?? []).Where(x =>
                    string.Equals(x.Properties?.CountryCode, "VN", StringComparison.OrdinalIgnoreCase) &&
                    x.Geometry?.Coordinates is { Length: >= 2 })
                .Select(ToLocation).OfType<LocationSearchResponse>()
                .DistinctBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            if (Cache.Count >= 500) Cache.Clear();
            Cache[term] = (results, DateTimeOffset.UtcNow.Add(results.Count == 0 ? TimeSpan.FromHours(1) : TimeSpan.FromDays(7)));
            return (results, null);
        }
        catch (HttpRequestException)
        {
            return (null, ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm tạm thời không khả dụng."));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm phản hồi quá chậm."));
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, ApiOutcome.Unavailable("Dịch vụ tra cứu địa điểm trả dữ liệu không hợp lệ."));
        }
        finally
        {
            Gate.Release();
        }
    }

    private static LocationSearchResponse? ToLocation(PhotonPlace place)
    {
        var coordinates = place.Geometry!.Coordinates!;
        var latitude = coordinates[1];
        var longitude = coordinates[0];
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180) return null;
        var street = string.Join(" ", new[] { place.Properties?.HouseNumber, place.Properties?.Street }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var names = new[] { place.Properties?.Name, street, place.Properties?.Locality, place.Properties?.District,
            place.Properties?.City, place.Properties?.Country };
        var displayName = string.Join(", ", names.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        if (string.IsNullOrWhiteSpace(displayName)) return null;
        return new LocationSearchResponse(displayName, latitude, longitude,
            !string.Equals(place.Properties?.Type, "house", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class PhotonResponse
    {
        [JsonPropertyName("features")] public List<PhotonPlace>? Features { get; set; }
    }
    private sealed class PhotonPlace
    {
        [JsonPropertyName("properties")] public PhotonProperties? Properties { get; set; }
        [JsonPropertyName("geometry")] public PhotonGeometry? Geometry { get; set; }
    }
    private sealed class PhotonProperties
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("street")] public string? Street { get; set; }
        [JsonPropertyName("housenumber")] public string? HouseNumber { get; set; }
        [JsonPropertyName("locality")] public string? Locality { get; set; }
        [JsonPropertyName("district")] public string? District { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("country")] public string? Country { get; set; }
        [JsonPropertyName("countrycode")] public string? CountryCode { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
    private sealed class PhotonGeometry
    {
        [JsonPropertyName("coordinates")] public double[]? Coordinates { get; set; }
    }
}
