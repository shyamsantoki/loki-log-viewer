using System.Net.Http.Json;
using System.Text.Json;
using LokiLogViewer.Shared.Search;

namespace LokiLogViewer.Client.Services;

public sealed class LogApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<SearchFieldDefinition>> GetSearchFieldsAsync(CancellationToken ct) =>
        GetAsync<IReadOnlyList<SearchFieldDefinition>>("api/logs/search-fields", ct);

    public Task<IReadOnlyList<string>> GetLabelsAsync(LabelsRequest request, CancellationToken ct) =>
        PostAsync<LabelsRequest, IReadOnlyList<string>>("api/logs/labels", request, ct);

    public Task<IReadOnlyList<string>> GetLabelValuesAsync(string labelName, LabelsRequest request, CancellationToken ct) =>
        PostAsync<LabelsRequest, IReadOnlyList<string>>($"api/logs/labels/{Uri.EscapeDataString(labelName)}/values", request, ct);

    public Task<LogQueryResponse> QueryAsync(LogQueryRequest request, CancellationToken ct) =>
        PostAsync<LogQueryRequest, LogQueryResponse>("api/logs/query", request, ct);

    private async Task<TResponse> GetAsync<TResponse>(string url, CancellationToken ct)
    {
        var response = await http.GetAsync(url, ct);
        return await ReadResponseAsync<TResponse>(response, ct);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest request, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(url, request, JsonOptions, ct);
        return await ReadResponseAsync<TResponse>(response, ct);
    }

    private async Task<TResponse> ReadResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct);
            throw new LogApiException(response.StatusCode, message);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct)
            ?? throw new LogApiException(response.StatusCode, "The server returned an empty response.");
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? body;
            }
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // Not a JSON body - fall through and return the raw text.
        }

        return string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Request failed." : body;
    }
}
