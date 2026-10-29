using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace UrbanReports.Api.Geocoding;

public interface INominatimClient
{
    Task<NominatimResponse?> ReverseAsync(double lat, double lng, CancellationToken ct);
}

public sealed class NominatimClient(IHttpClientFactory factory, IOptions<NominatimOptions> options, TimeProvider clock)
    : INominatimClient
{
    public const string HttpClientName = "Nominatim";

    private readonly string? _email = options.Value.Email;
    private readonly RequestThrottle _limiter = new(clock, options.Value.MinInterval, options.Value.MaxQueueWait);

    public Task<NominatimResponse?> ReverseAsync(double lat, double lng, CancellationToken ct) =>
        _limiter.RunAsync(async token =>
        {
            var url = string.Create(CultureInfo.InvariantCulture,
                $"reverse?format=jsonv2&lat={lat:0.#######}&lon={lng:0.#######}&zoom=18&addressdetails=1");
            if (!string.IsNullOrWhiteSpace(_email))
            {
                url += "&email=" + Uri.EscapeDataString(_email);
            }

            var http = factory.CreateClient(HttpClientName);
            using var response = await http.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NominatimResponse>(token);
        }, ct);
}
