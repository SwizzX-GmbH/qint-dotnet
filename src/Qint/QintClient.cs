using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Qint;

/// <summary>
/// A typed client for the Qint merchant API. Construct it with a Qint API key
/// (<c>qk_live_...</c>) and, optionally, a base URL and timeout. The client sends
/// <c>Authorization: Bearer &lt;key&gt;</c> on every request.
/// </summary>
/// <remarks>
/// The client is thread-safe and intended to be long-lived. When you pass your own
/// <see cref="HttpClient"/> (for example from <c>IHttpClientFactory</c>) the SDK does not
/// dispose it or mutate its <see cref="HttpClient.BaseAddress"/> / <see cref="HttpClient.Timeout"/>.
/// </remarks>
public sealed class QintClient : IDisposable
{
    /// <summary>Default API base URL (<c>https://api.qint.ch/api/v1</c>).</summary>
    public static readonly Uri DefaultBaseUrl = new("https://api.qint.ch/api/v1");

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly string UserAgent = "qint-dotnet/" + typeof(QintClient).Assembly.GetName().Version?.ToString(3);

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly Uri _baseUrl;
    private readonly string _apiKey;

    /// <summary>
    /// Creates a client that owns an internal <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="apiKey">Your Qint API key (<c>qk_live_...</c>).</param>
    /// <param name="baseUrl">Optional API base URL. Defaults to <see cref="DefaultBaseUrl"/>.</param>
    /// <param name="timeout">Optional per-request timeout. Defaults to 30 seconds.</param>
    public QintClient(string apiKey, Uri? baseUrl = null, TimeSpan? timeout = null)
        : this(apiKey, new HttpClient { Timeout = timeout ?? DefaultTimeout }, baseUrl, ownsHttpClient: true)
    {
    }

    /// <summary>
    /// Creates a client that reuses an injected <see cref="HttpClient"/> (for example from
    /// <c>IHttpClientFactory</c>). The SDK will not dispose the client.
    /// </summary>
    /// <param name="apiKey">Your Qint API key (<c>qk_live_...</c>).</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to send requests with.</param>
    /// <param name="baseUrl">Optional API base URL. Defaults to <see cref="DefaultBaseUrl"/>.</param>
    public QintClient(string apiKey, HttpClient httpClient, Uri? baseUrl = null)
        : this(apiKey, httpClient, baseUrl, ownsHttpClient: false)
    {
    }

    /// <summary>
    /// Creates a client with a string base URL.
    /// </summary>
    /// <param name="apiKey">Your Qint API key (<c>qk_live_...</c>).</param>
    /// <param name="baseUrl">API base URL as a string.</param>
    /// <param name="timeout">Optional per-request timeout. Defaults to 30 seconds.</param>
    public QintClient(string apiKey, string baseUrl, TimeSpan? timeout = null)
        : this(apiKey, new Uri(baseUrl), timeout)
    {
    }

    private QintClient(string apiKey, HttpClient httpClient, Uri? baseUrl, bool ownsHttpClient)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An API key is required.", nameof(apiKey));

        _apiKey = apiKey;
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
        _baseUrl = EnsureTrailingSlash(baseUrl ?? DefaultBaseUrl);
    }

    /// <summary>Creates a payment intent.</summary>
    /// <param name="request">The intent parameters (amount, currency, idempotency key and optional fields).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The created (or idempotently replayed) <see cref="Intent"/>.</returns>
    /// <exception cref="ArgumentException">The idempotency key is null, empty or whitespace.</exception>
    /// <exception cref="QintApiException">The API returned a non-2xx response.</exception>
    public async Task<Intent> CreateIntentAsync(CreateIntentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("An idempotency key is required.", nameof(request));

        var body = JsonSerializer.Serialize(request, QintJson.Options);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUrl, "intents"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        return await SendAsync<Intent>(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches a single payment intent by id.</summary>
    /// <param name="id">The intent id.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The <see cref="Intent"/>.</returns>
    /// <exception cref="QintApiException">The API returned a non-2xx response (for example 404).</exception>
    public async Task<Intent> GetIntentAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An intent id is required.", nameof(id));

        var uri = new Uri(_baseUrl, "intents/" + Uri.EscapeDataString(id));
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync<Intent>(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists payment intents, optionally filtered by status and paged.</summary>
    /// <param name="options">Optional filters and paging. Pass <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>A page of <see cref="Intent"/> results as an <see cref="IntentList"/>.</returns>
    /// <exception cref="QintApiException">The API returned a non-2xx response.</exception>
    public async Task<IntentList> ListIntentsAsync(ListIntentsOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildListUri(options));
        return await SendAsync<IntentList>(message, cancellationToken).ConfigureAwait(false);
    }

    private Uri BuildListUri(ListIntentsOptions? options)
    {
        var query = new List<string>(3);
        if (options?.Status is { } status)
            query.Add("status=" + Uri.EscapeDataString(StatusToWire(status)));
        if (options?.Page is { } page)
            query.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        if (options?.PageSize is { } pageSize)
            query.Add("pageSize=" + pageSize.ToString(CultureInfo.InvariantCulture));

        var relative = "intents";
        if (query.Count > 0)
            relative += "?" + string.Join("&", query);
        return new Uri(_baseUrl, relative);
    }

    private static string StatusToWire(IntentStatus status) =>
        JsonNamingPolicy.CamelCase.ConvertName(status.ToString());

    private async Task<T> SendAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (UserAgent is not null)
            message.Headers.UserAgent.TryParseAdd(UserAgent);

        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw BuildApiException((int)response.StatusCode, payload);

        T? result;
        try
        {
            result = JsonSerializer.Deserialize<T>(payload, QintJson.Options);
        }
        catch (JsonException ex)
        {
            throw new QintApiException((int)response.StatusCode, null,
                "Failed to deserialize the Qint API response: " + ex.Message);
        }

        if (result is null)
            throw new QintApiException((int)response.StatusCode, null, "The Qint API returned an empty response body.");

        return result;
    }

    private static QintApiException BuildApiException(int statusCode, string payload)
    {
        QintProblemDetails? problem = null;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                problem = JsonSerializer.Deserialize<QintProblemDetails>(payload, QintJson.Options);
            }
            catch (JsonException)
            {
                // Non-JSON error body — fall back to a generic message below.
            }
        }

        var message = problem?.Detail
                      ?? problem?.Title
                      ?? $"The Qint API request failed with HTTP status {statusCode}.";
        return new QintApiException(statusCode, problem, message);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var absolute = uri.AbsoluteUri;
        return absolute.EndsWith('/') ? uri : new Uri(absolute + "/");
    }

    /// <summary>Disposes the internal <see cref="HttpClient"/> when the client owns it.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}
