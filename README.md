# Qint .NET SDK

Official .NET SDK for the [Qint](https://qint.ch) merchant API — a thin, typed client
over the Qint public API for creating crypto payment intents and verifying webhooks.

- Typed `QintClient` over `HttpClient` (bring your own `HttpClient` for DI / testing)
- Records for every model, `System.Text.Json`, nullable-enabled, targets `net8.0`
- Constant-time webhook signature verification (`CryptographicOperations.FixedTimeEquals`)

Full API reference: **https://docs.qint.ch**

## Install

```bash
dotnet add package Qint
```

> **Note:** the package is not on NuGet.org yet (see [PUBLISH.md](PUBLISH.md) for the
> exact publish steps). Until it is, you can install straight from source today:
>
> ```bash
> git clone https://github.com/SwizzX-GmbH/qint-dotnet.git
> dotnet add reference ../qint-dotnet/src/Qint/Qint.csproj
> ```

## Quickstart (30 seconds)

Create an intent, redirect the buyer to the hosted checkout, then confirm the result by
polling `GetIntentAsync` or (better) by receiving a webhook.

```csharp
using Qint;

var qint = new QintClient("qk_live_...");

// 1. Create a payment intent.
var intent = await qint.CreateIntentAsync(new CreateIntentRequest
{
    Amount = 49.90m,
    Currency = "CHF",
    IdempotencyKey = "order-1001",  // required — unique per merchant; safe to retry
    Title = "Order #1001",
    ReturnUrl = "https://shop.example/thanks",
});

// 2. Send the buyer to the hosted checkout.
Console.WriteLine(intent.CheckoutUrl);   // https://checkout.qint.ch/pay/pi_...

// 3a. Poll for the outcome...
var latest = await qint.GetIntentAsync(intent.Id);
if (latest.Status == IntentStatus.Settled)
{
    // fulfil the order
}

// 3b. ...or list recent intents.
var page = await qint.ListIntentsAsync(new ListIntentsOptions
{
    Status = IntentStatus.Settled,
    Page = 1,
    PageSize = 20,
});
Console.WriteLine($"{page.Total} settled intents");
```

### Base URL and timeout

The client defaults to `https://qint-api.fly.dev/api/v1` (a dedicated `api.qint.ch`
endpoint is coming). Override the base URL or timeout when you need to:

```csharp
var qint = new QintClient("qk_live_...", baseUrl: new Uri("https://api.qint.ch/api/v1"),
    timeout: TimeSpan.FromSeconds(10));
```

### Bring your own `HttpClient` (DI / `IHttpClientFactory`)

```csharp
services.AddHttpClient();
services.AddSingleton(sp =>
    new QintClient("qk_live_...", sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
```

When you pass your own `HttpClient` the SDK does not dispose it or mutate its
`BaseAddress` / `Timeout`.

### Errors

Any non-2xx response throws `QintApiException`, which carries the HTTP status code and the
parsed RFC 7807 problem-details body:

```csharp
try
{
    await qint.CreateIntentAsync(new CreateIntentRequest
    {
        Amount = 5m,
        Currency = "CHF",
        IdempotencyKey = "order-5",
    });
}
catch (QintApiException ex)
{
    Console.Error.WriteLine($"{ex.StatusCode}: {ex.Detail}");
    // e.g. 403: This API key lacks the Write scope.
}
```

## Verifying webhooks

Qint delivers `payment.status` events to the endpoint you configure in the dashboard,
signed with your endpoint secret (`whsec_...`). Each delivery carries:

- `X-Qint-Signature: sha256=<hex HMAC-SHA256 of the raw body>`
- `X-Qint-Event-Id: <id>` — a stable delivery id. **Persist it and skip events you have
  already processed**, since Qint retries failed deliveries (each retry reuses the same
  event id). Always acknowledge with a fast `2xx`.

Verify against the **raw** request bytes — do not re-serialize first, or the signature
will not match.

### ASP.NET Core minimal API

```csharp
app.MapPost("/webhooks/qint", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();
    var signature = request.Headers["X-Qint-Signature"].ToString();

    if (!QintWebhooks.VerifyWebhookSignature(rawBody, signature, "whsec_..."))
        return Results.Unauthorized();

    var evt = QintWebhooks.ParseEvent(rawBody);

    // De-duplicate using the event id before doing any work.
    var eventId = request.Headers[QintWebhooks.EventIdHeaderName].ToString();
    // if (AlreadyProcessed(eventId)) return Results.Ok();

    if (evt.Status == IntentStatus.Settled)
    {
        // fulfil the order for evt.IntentId
    }

    return Results.Ok(); // ack fast
});
```

`ConstructEvent` does verify-then-parse in one call, throwing
`QintWebhookSignatureException` if the signature is invalid:

```csharp
var evt = QintWebhooks.ConstructEvent(rawBody, signature, "whsec_...");
```

## API surface

```csharp
// Client
new QintClient(string apiKey, Uri? baseUrl = null, TimeSpan? timeout = null);
new QintClient(string apiKey, HttpClient httpClient, Uri? baseUrl = null);
new QintClient(string apiKey, string baseUrl, TimeSpan? timeout = null);

Task<Intent>     QintClient.CreateIntentAsync(CreateIntentRequest request, CancellationToken ct = default);
Task<Intent>     QintClient.GetIntentAsync(string id, CancellationToken ct = default);
Task<IntentList> QintClient.ListIntentsAsync(ListIntentsOptions? options = null, CancellationToken ct = default);

// Webhooks
bool               QintWebhooks.VerifyWebhookSignature(string rawBody, string? signatureHeader, string secret);
bool               QintWebhooks.VerifyWebhookSignature(ReadOnlySpan<byte> rawBody, string? signatureHeader, string secret);
PaymentStatusEvent QintWebhooks.ParseEvent(string rawBody);
PaymentStatusEvent QintWebhooks.ConstructEvent(string rawBody, string? signatureHeader, string secret);
```

Statuses (`IntentStatus`): `Initiated`, `Pending`, `Confirmed`, `Settled`, `Failed`,
`Expired`, `Cancelled` — serialized on the wire as their lowercase names.

## Building and testing

```bash
dotnet build
dotnet test
```

The tests run fully offline (mocked `HttpMessageHandler`).

## License

[MIT](LICENSE) © SwizzX GmbH
