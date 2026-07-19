using System.Net;
using System.Text;
using System.Text.Json;
using Qint.Tests.Infrastructure;
using Xunit;

namespace Qint.Tests;

public class QintClientTests
{
    private const string ApiKey = "qk_live_test123";

    private const string SampleIntentJson = """
    {
      "id": "pi_abc123",
      "status": "initiated",
      "amount": 42.50,
      "currency": "CHF",
      "title": "Order 1001",
      "assetSymbol": null,
      "cryptoAmount": null,
      "depositAddress": null,
      "checkoutUrl": "https://checkout.qint.ch/pay/pi_abc123",
      "returnUrl": null,
      "createdAt": "2026-07-08T10:00:00+00:00",
      "expiresAt": "2026-07-08T10:30:00+00:00",
      "confirmedAt": null,
      "settledAt": null
    }
    """;

    private static QintClient ClientWith(MockHttpMessageHandler handler) =>
        new(ApiKey, new HttpClient(handler));

    // ── create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateIntentAsync_ShapesRequest_AndParsesResponse()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, SampleIntentJson);
        using var client = ClientWith(handler);

        var intent = await client.CreateIntentAsync(new CreateIntentRequest
        {
            Amount = 42.50m,
            Currency = "CHF",
            Title = "Order 1001",
            IdempotencyKey = "idem-1",
            ReturnUrl = "https://shop.example/thanks",
        });

        // Method + path.
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://qint-api.fly.dev/api/v1/intents", handler.LastRequest.RequestUri!.AbsoluteUri);

        // Auth header.
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal(ApiKey, handler.LastRequest.Headers.Authorization.Parameter);

        // Content type.
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);

        // Body fields.
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal(42.50m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("CHF", root.GetProperty("currency").GetString());
        Assert.Equal("Order 1001", root.GetProperty("title").GetString());
        Assert.Equal("idem-1", root.GetProperty("idempotencyKey").GetString());
        Assert.Equal("https://shop.example/thanks", root.GetProperty("returnUrl").GetString());

        // Parsed response.
        Assert.Equal("pi_abc123", intent.Id);
        Assert.Equal(IntentStatus.Initiated, intent.Status);
        Assert.Equal(42.50m, intent.Amount);
        Assert.Equal("CHF", intent.Currency);
        Assert.Equal("https://checkout.qint.ch/pay/pi_abc123", intent.CheckoutUrl);
    }

    [Fact]
    public async Task CreateIntentAsync_OmitsNullOptionalFields()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, SampleIntentJson);
        using var client = ClientWith(handler);

        await client.CreateIntentAsync(new CreateIntentRequest
        {
            Amount = 10m,
            Currency = "EUR",
            IdempotencyKey = "order-10",
        });

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.True(root.TryGetProperty("amount", out _));
        Assert.True(root.TryGetProperty("currency", out _));
        Assert.Equal("order-10", root.GetProperty("idempotencyKey").GetString());
        Assert.False(root.TryGetProperty("title", out _));
        Assert.False(root.TryGetProperty("returnUrl", out _));
    }

    [Fact]
    public async Task CreateIntentAsync_RejectsMissingIdempotencyKey()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, SampleIntentJson);
        using var client = ClientWith(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateIntentAsync(
            new CreateIntentRequest { Amount = 10m, Currency = "EUR", IdempotencyKey = "  " }));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task CreateIntentAsync_TreatsIdempotentReplay200AsSuccess()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, SampleIntentJson);
        using var client = ClientWith(handler);

        var intent = await client.CreateIntentAsync(new CreateIntentRequest
        {
            Amount = 10m,
            Currency = "USD",
            IdempotencyKey = "order-10",
        });

        Assert.Equal("pi_abc123", intent.Id);
    }

    // ── get ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIntentAsync_UsesIdInPath_AndSendsNoBody()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, SampleIntentJson);
        using var client = ClientWith(handler);

        var intent = await client.GetIntentAsync("pi_abc123");

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("https://qint-api.fly.dev/api/v1/intents/pi_abc123", handler.LastRequest.RequestUri!.AbsoluteUri);
        Assert.Null(handler.LastRequest.Content);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("pi_abc123", intent.Id);
    }

    // ── list ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListIntentsAsync_BuildsQueryString()
    {
        const string listJson = """
        { "items": [], "total": 0, "page": 2, "pageSize": 25 }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, listJson);
        using var client = ClientWith(handler);

        var page = await client.ListIntentsAsync(new ListIntentsOptions
        {
            Status = IntentStatus.Confirmed,
            Page = 2,
            PageSize = 25,
        });

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/intents", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("?status=confirmed&page=2&pageSize=25", handler.LastRequest.RequestUri.Query);
        Assert.Equal(2, page.Page);
        Assert.Equal(25, page.PageSize);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task ListIntentsAsync_NoOptions_SendsNoQuery()
    {
        const string listJson = """
        { "items": [], "total": 0, "page": 1, "pageSize": 20 }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, listJson);
        using var client = ClientWith(handler);

        await client.ListIntentsAsync();

        Assert.Equal("https://qint-api.fly.dev/api/v1/intents", handler.LastRequest!.RequestUri!.AbsoluteUri);
        Assert.Equal(string.Empty, handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task ListIntentsAsync_ParsesItems()
    {
        var listJson = $$"""
        { "items": [ {{SampleIntentJson}} ], "total": 1, "page": 1, "pageSize": 20 }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, listJson);
        using var client = ClientWith(handler);

        var page = await client.ListIntentsAsync();

        Assert.Single(page.Items);
        Assert.Equal("pi_abc123", page.Items[0].Id);
        Assert.Equal(1, page.Total);
    }

    // ── error handling ─────────────────────────────────────────────────────────

    [Fact]
    public async Task NonSuccess_ThrowsQintApiException_WithProblemDetail()
    {
        const string problemJson = """
        {
          "type": "https://httpstatuses.io/403",
          "title": "Forbidden",
          "status": 403,
          "detail": "This API key lacks the Write scope."
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.Forbidden, problemJson, "application/problem+json");
        using var client = ClientWith(handler);

        var ex = await Assert.ThrowsAsync<QintApiException>(
            () => client.CreateIntentAsync(new CreateIntentRequest
            {
                Amount = 5m,
                Currency = "CHF",
                IdempotencyKey = "order-5",
            }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal("This API key lacks the Write scope.", ex.Detail);
        Assert.Equal("This API key lacks the Write scope.", ex.Message);
        Assert.Equal("Forbidden", ex.ProblemDetails!.Title);
    }

    [Fact]
    public async Task NonSuccess_NonJsonBody_StillThrowsWithStatus()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadGateway, "upstream boom", "text/plain");
        using var client = ClientWith(handler);

        var ex = await Assert.ThrowsAsync<QintApiException>(() => client.GetIntentAsync("pi_x"));

        Assert.Equal(502, ex.StatusCode);
        Assert.Null(ex.ProblemDetails);
        Assert.Contains("502", ex.Message);
    }

    // ── construction guards ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_RejectsEmptyApiKey()
    {
        Assert.Throws<ArgumentException>(() => new QintClient("  "));
    }

    [Fact]
    public void DefaultBaseUrl_IsTheFlyEndpoint()
    {
        Assert.Equal("https://qint-api.fly.dev/api/v1", QintClient.DefaultBaseUrl.AbsoluteUri);
    }

    [Fact]
    public async Task CustomBaseUrl_IsHonored()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, SampleIntentJson);
        using var client = new QintClient(ApiKey, new HttpClient(handler), new Uri("https://api.qint.ch/api/v1"));

        await client.GetIntentAsync("pi_abc123");

        Assert.Equal("https://api.qint.ch/api/v1/intents/pi_abc123", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }
}
