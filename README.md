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
    Currency = "CHF",               // CHF, EUR, USD or GBP (QintCurrencies.*)
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

### Currencies that are switched on

Qint can switch a fiat currency off for new payments (and back on) at any time. Creating an
intent in a disabled currency throws a plain `400` (no `Code`) whose `Detail` names the
enabled ones, e.g. `GBP is currently disabled on Qint. currency must be one of: CHF, EUR, USD.`
Read the live list from the public `GET /api/v1/public/currencies` endpoint and hide
disabled currencies in your checkout. The response is cacheable for 30 seconds — cache it
on your side rather than calling it on every checkout.

```csharp
var currencies = await qint.ListCurrenciesAsync();
IReadOnlyList<string> offered = currencies.EnabledCodes();   // e.g. ["CHF", "EUR", "USD"]
if (!currencies.IsEnabled(QintCurrencies.Gbp))
{
    // don't offer GBP right now
}
```

### Base URL and timeout

The client defaults to `https://api.qint.ch/api/v1` — the stable production
hostname. Override the base URL or timeout when you need to:

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

Any non-2xx response throws `QintApiException`, which carries the HTTP status code, the
parsed RFC 7807 problem-details body and, when the API sends one, a machine-readable
`Code`:

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

### Payment limits

Each payment must be between CHF 10 and CHF 100'000 (or the equivalent in EUR, USD or
GBP). Outside that range `CreateIntentAsync` throws a `400` with
`ex.Code == QintErrorCodes.AmountOutOfRange` (`"amount_out_of_range"`) and the numbers on
`ex.ProblemDetails.MinChf`, `MaxChf` and `AmountChf`. If no exchange rate is available to
judge a non-CHF amount it throws a `503` with `ex.Code == QintErrorCodes.RateUnavailable`
(`"rate_unavailable"`) — retry in a minute. Your agreed cap, if Qint set one, replaces the
CHF 100'000 maximum. `ex.TraceId` is the request correlation id to quote to support; any
other problem members are in `ex.ProblemDetails.Extensions`.

```csharp
catch (QintApiException ex) when (ex.Code == QintErrorCodes.AmountOutOfRange)
{
    var p = ex.ProblemDetails!;
    Console.Error.WriteLine($"{ex.Detail} (allowed CHF {p.MinChf}–{p.MaxChf}, got CHF {p.AmountChf})");
}
```

### Account Currency and the credit

Your balance is held in fiat, in your **Account Currency** (CHF by default; EUR, USD or GBP
are possible). The amount a payment adds to it is fixed when the buyer is quoted, never by a
rate read at settlement. Once a payment is settled and credited, `Intent.CreditAmount` is the
amount added to your balance (2 decimal places), `Intent.CreditCurrency` the currency it is in
(your Account Currency at quote time), and `Intent.CreditRate` the units of `CreditCurrency`
per 1 unit of the invoice `Currency` (`1` when they match). `FeeAmount` (Qint's fee) and
`NetAmount` (what you are credited, in the intent's currency) are set once the payment is
settled; all of these are `null` before, and the three credit fields stay `null` on payments
settled before the fiat balance (20 October 2026). The `payment.status` webhook carries the
same three credit fields, filled on the settled event.

### Underpaid payments

A payment that comes up short but within your accepted underpayment still settles. The
`payment.status` webhook then carries `Underpaid = true`, `ReceivedAmount` (the fiat amount
actually paid, in the intent's currency) and `UnderpaidBps` (how short, in basis points of
the invoice); both are `null` otherwise. `Intent` carries the same `Underpaid` flag and the
two amounts, plus a `Reversed` flag and `ReversedAt`. `Reversed == true` means the payment
was reversed after settlement: treat it as not paid even though its status stays `Settled`.

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

`ParseEvent` / `ConstructEvent` only accept `payment.status`. The dashboard's
**Send test event** button delivers a signed `ping`, and Qint may add event types later —
to handle every delivery use `ConstructWebhook` (or `ParseWebhook`), which never fails on
an unknown type:

```csharp
var webhook = QintWebhooks.ConstructWebhook(rawBody, signature, "whsec_...");
switch (webhook.Type)
{
    case QintWebhooks.PaymentStatusEventType:
        var evt = webhook.PaymentStatus!;   // fulfil on evt.Status == Settled && !evt.Reversed
        break;
    case QintWebhooks.PingEventType:
        break;                              // test event: just acknowledge
    default:
        break;                              // unknown type: acknowledge and ignore (webhook.Raw has the body)
}
return Results.Ok();
```

## API surface

```csharp
// Client
new QintClient(string apiKey, Uri? baseUrl = null, TimeSpan? timeout = null);
new QintClient(string apiKey, HttpClient httpClient, Uri? baseUrl = null);
new QintClient(string apiKey, string baseUrl, TimeSpan? timeout = null);

Task<Intent>     QintClient.CreateIntentAsync(CreateIntentRequest request, CancellationToken ct = default);
Task<Intent>     QintClient.GetIntentAsync(string id, CancellationToken ct = default);
Task<IntentList>   QintClient.ListIntentsAsync(ListIntentsOptions? options = null, CancellationToken ct = default);
Task<CurrencyList> QintClient.ListCurrenciesAsync(CancellationToken ct = default);   // public, no scope needed

// Webhooks
bool               QintWebhooks.VerifyWebhookSignature(string rawBody, string? signatureHeader, string secret);
bool               QintWebhooks.VerifyWebhookSignature(ReadOnlySpan<byte> rawBody, string? signatureHeader, string secret);
PaymentStatusEvent QintWebhooks.ParseEvent(string rawBody);
PaymentStatusEvent QintWebhooks.ConstructEvent(string rawBody, string? signatureHeader, string secret);
WebhookEvent       QintWebhooks.ParseWebhook(string rawBody);
WebhookEvent       QintWebhooks.ConstructWebhook(string rawBody, string? signatureHeader, string secret);

// Constants
QintCurrencies.{Chf, Eur, Usd, Gbp, All}
QintErrorCodes.{AmountOutOfRange, RateUnavailable, SettlementAssetMismatch}
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
