using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Qint.Tests;

public class QintWebhooksTests
{
    private const string Secret = "whsec_test_secret";

    private const string EventBody = """
    {
      "type": "payment.status",
      "intentId": "pi_abc123",
      "status": "confirmed",
      "amount": 42.50,
      "currency": "CHF",
      "assetSymbol": "USDT",
      "underpaid": false,
      "expectedCryptoAmount": 42.510000,
      "receivedCryptoAmount": 42.510000,
      "invoiceId": null,
      "paymentLinkId": "pl_1",
      "occurredAt": "2026-07-08T10:15:00+00:00"
    }
    """;

    /// <summary>Reference signer, matching the Qint delivery worker (key = secret, message = raw body).</summary>
    private static string Sign(string body, string secret) =>
        "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();

    [Fact]
    public void VerifyWebhookSignature_ValidSignature_ReturnsTrue()
    {
        var signature = Sign(EventBody, Secret);
        Assert.True(QintWebhooks.VerifyWebhookSignature(EventBody, signature, Secret));
    }

    [Fact]
    public void VerifyWebhookSignature_UppercaseHex_StillValid()
    {
        var signature = Sign(EventBody, Secret).ToUpperInvariant().Replace("SHA256=", "sha256=");
        Assert.True(QintWebhooks.VerifyWebhookSignature(EventBody, signature, Secret));
    }

    [Fact]
    public void VerifyWebhookSignature_TamperedBody_ReturnsFalse()
    {
        var signature = Sign(EventBody, Secret);
        var tamperedBody = EventBody.Replace("42.50", "9999.00");
        Assert.False(QintWebhooks.VerifyWebhookSignature(tamperedBody, signature, Secret));
    }

    [Fact]
    public void VerifyWebhookSignature_TamperedSignature_ReturnsFalse()
    {
        var signature = Sign(EventBody, Secret);
        // Flip the last hex digit.
        var last = signature[^1] == '0' ? '1' : '0';
        var tampered = signature[..^1] + last;
        Assert.False(QintWebhooks.VerifyWebhookSignature(EventBody, tampered, Secret));
    }

    [Fact]
    public void VerifyWebhookSignature_WrongSecret_ReturnsFalse()
    {
        var signature = Sign(EventBody, Secret);
        Assert.False(QintWebhooks.VerifyWebhookSignature(EventBody, signature, "whsec_wrong"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("deadbeef")]              // missing "sha256=" prefix
    [InlineData("sha256=nothex!!")]       // invalid hex
    public void VerifyWebhookSignature_MalformedHeader_ReturnsFalse(string header)
    {
        Assert.False(QintWebhooks.VerifyWebhookSignature(EventBody, header, Secret));
    }

    [Fact]
    public void VerifyWebhookSignature_NullHeader_ReturnsFalse()
    {
        Assert.False(QintWebhooks.VerifyWebhookSignature(EventBody, null, Secret));
    }

    [Fact]
    public void ParseEvent_MapsAllFields()
    {
        var evt = QintWebhooks.ParseEvent(EventBody);

        Assert.Equal("payment.status", evt.Type);
        Assert.Equal("pi_abc123", evt.IntentId);
        Assert.Equal(IntentStatus.Confirmed, evt.Status);
        Assert.Equal(42.50m, evt.Amount);
        Assert.Equal("CHF", evt.Currency);
        Assert.Equal("USDT", evt.AssetSymbol);
        Assert.False(evt.Underpaid);
        Assert.Equal(42.51m, evt.ExpectedCryptoAmount);
        Assert.Equal(42.51m, evt.ReceivedCryptoAmount);
        Assert.Null(evt.InvoiceId);
        Assert.Equal("pl_1", evt.PaymentLinkId);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 10, 15, 0, TimeSpan.Zero), evt.OccurredAt);
    }

    [Fact]
    public void ParseEvent_AcceptedUnderpayment_MapsNewFields()
    {
        const string body = """
        {
          "type": "payment.status",
          "intentId": "pi_short",
          "status": "settled",
          "amount": 100.00,
          "currency": "GBP",
          "assetSymbol": "USDT",
          "underpaid": true,
          "receivedAmount": 98.00,
          "underpaidBps": 200,
          "expectedCryptoAmount": "126.58227848",
          "receivedCryptoAmount": "124.05063291",
          "invoiceId": null,
          "paymentLinkId": null,
          "reversed": false,
          "reversedAt": null,
          "creditAmount": 109.83,
          "creditCurrency": "CHF",
          "creditRate": 1.1207,
          "occurredAt": "2026-07-08T10:15:00+00:00"
        }
        """;

        var evt = QintWebhooks.ParseEvent(body);

        Assert.Equal("GBP", evt.Currency);
        Assert.Equal(109.83m, evt.CreditAmount);
        Assert.Equal("CHF", evt.CreditCurrency);
        Assert.Equal(1.1207m, evt.CreditRate);
        Assert.True(evt.Underpaid);
        Assert.Equal(98.00m, evt.ReceivedAmount);
        Assert.Equal(200, evt.UnderpaidBps);
        Assert.Equal(126.58227848m, evt.ExpectedCryptoAmount);
        Assert.Equal(124.05063291m, evt.ReceivedCryptoAmount);
        Assert.False(evt.Reversed);
        Assert.Null(evt.ReversedAt);
    }

    [Fact]
    public void ParseEvent_WithoutUnderpaymentFields_LeavesThemNull()
    {
        var evt = QintWebhooks.ParseEvent(EventBody);

        Assert.Null(evt.ReceivedAmount);
        Assert.Null(evt.UnderpaidBps);
        Assert.Null(evt.ReversedAt);
        Assert.False(evt.Reversed);
        Assert.Null(evt.CreditAmount);
        Assert.Null(evt.CreditCurrency);
        Assert.Null(evt.CreditRate);
    }

    [Fact]
    public void ConstructEvent_ValidSignature_ReturnsParsedEvent()
    {
        var signature = Sign(EventBody, Secret);
        var evt = QintWebhooks.ConstructEvent(EventBody, signature, Secret);
        Assert.Equal("pi_abc123", evt.IntentId);
        Assert.Equal(IntentStatus.Confirmed, evt.Status);
    }

    [Fact]
    public void ConstructEvent_BadSignature_Throws()
    {
        Assert.Throws<QintWebhookSignatureException>(
            () => QintWebhooks.ConstructEvent(EventBody, "sha256=deadbeef", Secret));
    }

    // ── ping + unknown event types ───────────────────────────────────────────

    private const string PingBody = """
    {"type":"ping","endpointId":"3f2c1a9e-5b7d-4e8f-9a1b-2c3d4e5f6a7b","sentAt":"2026-10-14T12:00:00+00:00"}
    """;

    [Fact]
    public void ParseWebhook_PaymentStatus_IsTyped()
    {
        var evt = QintWebhooks.ParseWebhook(EventBody);

        Assert.Equal(QintWebhooks.PaymentStatusEventType, evt.Type);
        Assert.NotNull(evt.PaymentStatus);
        Assert.Null(evt.Ping);
        Assert.Equal("pi_abc123", evt.PaymentStatus!.IntentId);
    }

    [Fact]
    public void ParseWebhook_Ping_IsTyped()
    {
        var evt = QintWebhooks.ParseWebhook(PingBody);

        Assert.Equal(QintWebhooks.PingEventType, evt.Type);
        Assert.Null(evt.PaymentStatus);
        Assert.NotNull(evt.Ping);
        Assert.Equal("3f2c1a9e-5b7d-4e8f-9a1b-2c3d4e5f6a7b", evt.Ping!.EndpointId);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero), evt.Ping.SentAt);
    }

    [Fact]
    public void ParseWebhook_UnknownType_IsTolerated()
    {
        const string body = """{"type":"payout.status","payoutId":"po_1","sourceAsset":"USDC"}""";

        var evt = QintWebhooks.ParseWebhook(body);

        Assert.Equal("payout.status", evt.Type);
        Assert.Null(evt.PaymentStatus);
        Assert.Null(evt.Ping);
        Assert.Equal("USDC", evt.Raw.GetProperty("sourceAsset").GetString());
    }

    [Fact]
    public void ConstructWebhook_VerifiesPing()
    {
        var evt = QintWebhooks.ConstructWebhook(PingBody, Sign(PingBody, Secret), Secret);
        Assert.NotNull(evt.Ping);

        Assert.Throws<QintWebhookSignatureException>(
            () => QintWebhooks.ConstructWebhook(PingBody, "sha256=deadbeef", Secret));
    }

    [Fact]
    public void ParseEvent_Ping_ThrowsWithPointerToParseWebhook()
    {
        var ex = Assert.Throws<System.Text.Json.JsonException>(() => QintWebhooks.ParseEvent(PingBody));
        Assert.Contains("ParseWebhook", ex.Message);
    }

    [Fact]
    public void ParseEvent_UnknownFields_AreTolerated()
    {
        var body = EventBody.Replace("\"type\": \"payment.status\",", "\"type\": \"payment.status\", \"futureField\": \"x\", \"newThing\": [1],");
        var evt = QintWebhooks.ParseEvent(body);
        Assert.Equal("pi_abc123", evt.IntentId);
    }
}

