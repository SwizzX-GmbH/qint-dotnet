# Changelog

All notable changes to the Qint .NET SDK. Versions follow [semver](https://semver.org/).

## 0.2.0 — 2026-10-14

Brings the SDK in line with the Qint merchant API as of 14 October 2026.

### Added
- `QintClient.ListCurrenciesAsync()` — `GET /api/v1/public/currencies`: every fiat currency
  and whether it is offered for new payments right now. `CurrencyList.EnabledCodes()` and
  `CurrencyList.IsEnabled(code)` help hide disabled currencies. Cacheable for 30 seconds.
- `Intent.CreditAsset` — the stablecoin the credit was booked in (`USDC` for merchants
  registered in the EU, `USDT` otherwise), and `QintSettlementAssets` (`Usdt`, `Usdc`).
  Kept as a string, so a future asset still deserializes.
- `Intent.Reversed` and `Intent.Underpaid` flags (the API sends them on every intent).
- Webhooks: `QintWebhooks.ParseWebhook` / `ConstructWebhook` return a `WebhookEvent` that
  types `payment.status` and the dashboard's `ping` test event (`PingEvent`) and tolerates
  unknown event types (`WebhookEvent.Raw`). `PaymentStatusEventType` / `PingEventType`
  constants.
- `QintErrorCodes.SettlementAssetMismatch` (`settlement_asset_mismatch`, returned by the
  dashboard API's payout endpoints; listed for completeness).
- `QintProblemDetails.TraceId` and `QintApiException.TraceId`.
- README: currency availability, settlement asset, disabled-currency `400`.

### Changed
- `traceId` is now a typed property and no longer appears in `QintProblemDetails.Extensions`.
- `ParseEvent` / `ConstructEvent` on a non-`payment.status` body (e.g. `ping`) now throw a
  `JsonException` that names the event type and points to `ParseWebhook`.
- Doc fixes: `FeeAmount` / `NetAmount` are null until the payment is settled;
  `CreditUsdt` / `CreditRate` are in `CreditAsset` (the name predates USDC).

## 0.1.0

Initial release: typed `QintClient` (create / get / list intents), webhook signature
verification, GBP, the accepted-underpayment intent fields and typed problem codes.
