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
        Assert.Equal("https://api.qint.ch/api/v1/intents", handler.LastRequest.RequestUri!.AbsoluteUri);

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
        Assert.Equal("https://api.qint.ch/api/v1/intents/pi_abc123", handler.LastRequest.RequestUri!.AbsoluteUri);
        Assert.Null(handler.LastRequest.Content);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("pi_abc123", intent.Id);
    }

    [Fact]
    public async Task GetIntentAsync_ParsesFeeUnderpaymentAndReversalFields()
    {
        const string json = """
        {
          "id": "pi_abc123",
          "status": "settled",
          "amount": 100.00,
          "currency": "GBP",
          "checkoutUrl": "https://checkout.qint.ch/pay/pi_abc123",
          "createdAt": "2026-07-08T10:00:00+00:00",
          "expiresAt": "2026-07-08T10:30:00+00:00",
          "settledAt": "2026-07-08T10:20:00+00:00",
          "feeAmount": 0.98,
          "netAmount": 97.02,
          "settledAmount": null,
          "receivedAmount": 98.00,
          "underpaidBps": 200,
          "creditUsdt": 108.9,
          "creditRate": 0.8909,
          "reversedAt": "2026-07-09T09:00:00+00:00"
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var client = ClientWith(handler);

        var intent = await client.GetIntentAsync("pi_abc123");

        Assert.Equal(QintCurrencies.Gbp, intent.Currency);
        Assert.Equal(0.98m, intent.FeeAmount);
        Assert.Equal(97.02m, intent.NetAmount);
        Assert.Null(intent.SettledAmount);
        Assert.Equal(98.00m, intent.ReceivedAmount);
        Assert.Equal(200, intent.UnderpaidBps);
        Assert.Equal(108.9m, intent.CreditUsdt);
        Assert.Equal(0.8909m, intent.CreditRate);
        Assert.Equal(new DateTimeOffset(2026, 7, 9, 9, 0, 0, TimeSpan.Zero), intent.ReversedAt);
    }

    [Fact]
    public async Task GetIntentAsync_WithoutNewFields_LeavesThemNull()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, SampleIntentJson);
        using var client = ClientWith(handler);

        var intent = await client.GetIntentAsync("pi_abc123");

        Assert.Null(intent.FeeAmount);
        Assert.Null(intent.NetAmount);
        Assert.Null(intent.SettledAmount);
        Assert.Null(intent.ReceivedAmount);
        Assert.Null(intent.UnderpaidBps);
        Assert.Null(intent.CreditUsdt);
        Assert.Null(intent.CreditRate);
        Assert.Null(intent.ReversedAt);
    }

    [Fact]
    public async Task CreateIntentAsync_AcceptsGbp()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.Created,
            SampleIntentJson.Replace("\"CHF\"", "\"GBP\""));
        using var client = ClientWith(handler);

        var intent = await client.CreateIntentAsync(new CreateIntentRequest
        {
            Amount = 25m,
            Currency = QintCurrencies.Gbp,
            IdempotencyKey = "order-gbp",
        });

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("GBP", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal("GBP", intent.Currency);
        Assert.Equal(new[] { "CHF", "EUR", "USD", "GBP" }, QintCurrencies.All);
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

        Assert.Equal("https://api.qint.ch/api/v1/intents", handler.LastRequest!.RequestUri!.AbsoluteUri);
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
        Assert.Null(ex.Code);
    }

    [Fact]
    public async Task AmountOutOfRange_ExposesCodeAndLimits()
    {
        const string problemJson = """
        {
          "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
          "title": "Amount out of range",
          "status": 400,
          "detail": "Payments must be between CHF 10 and CHF 100'000 (or the equivalent). This amount is CHF 5.00.",
          "traceId": "00-abc-def-00",
          "code": "amount_out_of_range",
          "minChf": 10,
          "maxChf": 100000,
          "amountChf": 5.00
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, problemJson, "application/problem+json");
        using var client = ClientWith(handler);

        var ex = await Assert.ThrowsAsync<QintApiException>(
            () => client.CreateIntentAsync(new CreateIntentRequest
            {
                Amount = 5m,
                Currency = "CHF",
                IdempotencyKey = "order-5",
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(QintErrorCodes.AmountOutOfRange, ex.Code);
        Assert.Equal(10m, ex.ProblemDetails!.MinChf);
        Assert.Equal(100000m, ex.ProblemDetails.MaxChf);
        Assert.Equal(5.00m, ex.ProblemDetails.AmountChf);
        Assert.Equal("00-abc-def-00", ex.TraceId);
        Assert.Equal("00-abc-def-00", ex.ProblemDetails.TraceId);
        Assert.Equal(
            "Payments must be between CHF 10 and CHF 100'000 (or the equivalent). This amount is CHF 5.00.",
            ex.Message);
    }

    [Fact]
    public async Task RateUnavailable_ExposesCode()
    {
        const string problemJson = """
        {
          "title": "Exchange rate unavailable",
          "status": 503,
          "detail": "Exchange rate unavailable — try again in a minute.",
          "code": "rate_unavailable"
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable, problemJson, "application/problem+json");
        using var client = ClientWith(handler);

        var ex = await Assert.ThrowsAsync<QintApiException>(
            () => client.CreateIntentAsync(new CreateIntentRequest
            {
                Amount = 50m,
                Currency = QintCurrencies.Gbp,
                IdempotencyKey = "order-50",
            }));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal(QintErrorCodes.RateUnavailable, ex.Code);
        Assert.Null(ex.ProblemDetails!.MinChf);
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
        Assert.Equal("https://api.qint.ch/api/v1", QintClient.DefaultBaseUrl.AbsoluteUri);
    }

    [Fact]
    public async Task CustomBaseUrl_IsHonored()
    {
        // A base DISTINCT from the default — this test is vacuous otherwise.
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, SampleIntentJson);
        using var client = new QintClient(ApiKey, new HttpClient(handler), new Uri("https://api.example.test/v1"));

        await client.GetIntentAsync("pi_abc123");

        Assert.Equal("https://api.example.test/v1/intents/pi_abc123", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    // ── 2026-10 API sync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetIntentAsync_SettledUsdcIntent_MapsCreditAssetAndFlags()
    {
        const string json = """
        {
          "id": "pi_eu1", "status": "settled", "amount": 100.00, "currency": "EUR",
          "title": null, "assetSymbol": "USDC", "cryptoAmount": "108.90000000",
          "depositAddress": "0xabc", "checkoutUrl": "https://checkout.qint.ch/pay/pi_eu1",
          "returnUrl": null, "createdAt": "2026-10-14T09:00:00+00:00",
          "expiresAt": "2026-10-14T09:15:00+00:00", "confirmedAt": "2026-10-14T09:05:00+00:00",
          "settledAt": "2026-10-14T09:06:00+00:00", "reversedAt": null,
          "reversed": false, "underpaid": true,
          "feeAmount": 0.50, "netAmount": 99.20, "settledAmount": 99.70,
          "receivedAmount": 99.70, "underpaidBps": 30,
          "creditUsdt": 108.123456, "creditRate": 0.9175, "creditAsset": "USDC",
          "someFutureField": {"nested": [1, 2, 3]}
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var client = ClientWith(handler);

        var intent = await client.GetIntentAsync("pi_eu1");

        Assert.Equal(IntentStatus.Settled, intent.Status);
        Assert.Equal(QintSettlementAssets.Usdc, intent.CreditAsset);
        Assert.Equal(108.123456m, intent.CreditUsdt);
        Assert.Equal(0.9175m, intent.CreditRate);
        Assert.Equal(0.50m, intent.FeeAmount);
        Assert.Equal(99.20m, intent.NetAmount);
        Assert.True(intent.Underpaid);
        Assert.False(intent.Reversed);
        Assert.Equal(30, intent.UnderpaidBps);
    }

    [Fact]
    public async Task GetIntentAsync_UnsettledIntent_LeavesEconomicsNull()
    {
        const string json = """
        {
          "id": "pi_abc123", "status": "pending", "amount": 42.50, "currency": "GBP",
          "checkoutUrl": "https://checkout.qint.ch/pay/pi_abc123",
          "createdAt": "2026-07-08T10:00:00+00:00", "expiresAt": "2026-07-08T10:15:00+00:00",
          "reversed": false, "underpaid": false,
          "feeAmount": null, "netAmount": null, "settledAmount": null, "receivedAmount": null,
          "underpaidBps": null, "creditUsdt": null, "creditRate": null, "creditAsset": null
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var client = ClientWith(handler);

        var intent = await client.GetIntentAsync("pi_abc123");

        Assert.Equal(QintCurrencies.Gbp, intent.Currency);
        Assert.Null(intent.FeeAmount);
        Assert.Null(intent.NetAmount);
        Assert.Null(intent.CreditUsdt);
        Assert.Null(intent.CreditRate);
        Assert.Null(intent.CreditAsset);
        Assert.False(intent.Underpaid);
        Assert.False(intent.Reversed);
    }

    [Fact]
    public async Task GetIntentAsync_UnknownSettlementAsset_IsTolerated()
    {
        var json = SampleIntentJson.Replace("\"settledAt\": null", "\"settledAt\": null, \"creditAsset\": \"EURC\", \"creditUsdt\": 1.5");
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var client = ClientWith(handler);

        var intent = await client.GetIntentAsync("pi_abc123");

        Assert.Equal("EURC", intent.CreditAsset);
        Assert.Equal(1.5m, intent.CreditUsdt);
    }

    [Fact]
    public async Task ListCurrenciesAsync_HitsPublicEndpoint_AndParses()
    {
        const string json = """
        {"currencies":[{"code":"CHF","enabled":true},{"code":"EUR","enabled":true},
                       {"code":"USD","enabled":true},{"code":"GBP","enabled":false},
                       {"code":"JPY","enabled":true,"label":"future field"}]}
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var client = ClientWith(handler);

        var list = await client.ListCurrenciesAsync();

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("https://api.qint.ch/api/v1/public/currencies", handler.LastRequest.RequestUri!.AbsoluteUri);
        Assert.Equal(5, list.Currencies.Count);
        Assert.Equal(new[] { "CHF", "EUR", "USD", "JPY" }, list.EnabledCodes());
        Assert.True(list.IsEnabled("chf"));
        Assert.False(list.IsEnabled(QintCurrencies.Gbp));
        Assert.False(list.IsEnabled("XYZ"));
    }

    [Fact]
    public async Task ListCurrenciesAsync_CustomBaseUrl_ResolvesUnderIt()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, """{"currencies":[]}""");
        using var client = new QintClient(ApiKey, new HttpClient(handler), new Uri("https://api.example.test/v1"));

        var list = await client.ListCurrenciesAsync();

        Assert.Equal("https://api.example.test/v1/public/currencies", handler.LastRequest!.RequestUri!.AbsoluteUri);
        Assert.Empty(list.EnabledCodes());
    }

    [Fact]
    public async Task CreateIntentAsync_DisabledCurrency_SurfacesPlain400Detail()
    {
        const string problemJson = """
        {
          "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
          "title": "An error occurred while processing your request.",
          "status": 400,
          "detail": "GBP is currently disabled on Qint. currency must be one of: CHF, EUR, USD.",
          "traceId": "00-1-2-00"
        }
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, problemJson, "application/problem+json");
        using var client = ClientWith(handler);

        var ex = await Assert.ThrowsAsync<QintApiException>(
            () => client.CreateIntentAsync(new CreateIntentRequest
            {
                Amount = 50m,
                Currency = QintCurrencies.Gbp,
                IdempotencyKey = "order-gbp",
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Null(ex.Code);
        Assert.Equal("GBP is currently disabled on Qint. currency must be one of: CHF, EUR, USD.", ex.Detail);
        Assert.Equal(ex.Detail, ex.Message);
        Assert.Equal("00-1-2-00", ex.TraceId);
    }

    [Fact]
    public async Task SettlementAssetMismatch_CodeIsExposed()
    {
        const string problemJson = """
        {"title":"Wrong payout asset","status":400,"detail":"This merchant settles in USDC.","code":"settlement_asset_mismatch"}
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, problemJson, "application/problem+json");
        using var client = ClientWith(handler);

        var ex = await Assert.ThrowsAsync<QintApiException>(() => client.GetIntentAsync("pi_x"));

        Assert.Equal(QintErrorCodes.SettlementAssetMismatch, ex.Code);
        Assert.Equal("settlement_asset_mismatch", QintErrorCodes.SettlementAssetMismatch);
    }

    [Fact]
    public void SettlementAssets_ListUsdtAndUsdc()
    {
        Assert.Equal(new[] { "USDT", "USDC" }, QintSettlementAssets.All);
    }
}

