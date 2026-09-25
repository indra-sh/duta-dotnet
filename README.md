# Duta.Net

The official .NET SDK for [Duta](https://duta.indra.sh), transactional email
for Malaysia.

- .NET 8+. Async throughout, with `CancellationToken`.
- `services.AddDuta(...)` for ASP.NET Core and any host with dependency injection.
- Retries rate limits and server errors safely: every send carries an
  idempotency key, so a retry can never send twice.

## Upgrading from 0.1.x

0.2.0 is a new SDK for Duta's current API, not an update of 0.1.x, which was
written for an earlier version of Duta that no longer runs.

## Install

```sh
dotnet add package Duta.Net
```

## ASP.NET Core

```csharp
builder.Services.AddDuta(o => o.ApiKey = builder.Configuration["Duta:ApiKey"]);
```

Then inject `DutaClient` wherever you send mail:

```csharp
app.MapPost("/orders/{id}/receipt", async (string id, DutaClient duta) =>
{
    var sent = await duta.Emails.SendAsync(new SendEmailRequest
    {
        From = "Kedai <resit@kedai.my>",
        To = ["siti@example.com"],
        Subject = "Resit #1042",
        Html = "<p>Terima kasih.</p>",
    }, new SendOptions(IdempotencyKey: $"receipt-{id}"));
    return sent.Id;
});
```

## Without dependency injection

```csharp
var duta = new DutaClient(Environment.GetEnvironmentVariable("DUTA_API_KEY"));
var sent = await duta.Emails.SendAsync(email);
```

Keep one `DutaClient` for the life of the app. Errors throw `DutaException`:
`Code` is Duta's [error code](https://docs.duta.indra.sh/guides/errors/) and
`RequestId` finds the request on the Logs screen.

```csharp
try
{
    await duta.Emails.SendAsync(email);
}
catch (DutaException e)
{
    logger.LogError("Send failed: {Code} {RequestId}", e.Code, e.RequestId);
}
```

## Batch and paging

```csharp
await duta.Batch.SendAsync([first, second], new BatchOptions(Validation: "permissive"));

await foreach (var email in duta.Emails.ListAllAsync(new EmailListParams { Status = "bounced" }))
    Console.WriteLine(email.Id);
```

## Verify webhooks

```csharp
var body = await new StreamReader(Request.Body).ReadToEndAsync(); // the raw body
var evt = WebhookSignature.Verify(
    body,
    Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
    configuration["Duta:WebhookSecret"]);
```

It throws `WebhookVerificationException` when the signature is wrong or the
delivery is more than five minutes old.

## Everything else

| | |
|---|---|
| `Emails` | `SendAsync`, `GetAsync`, `ListAsync`, `ListAllAsync` |
| `Batch` | `SendAsync` |
| `Domains` | `CreateAsync`, `ListAsync`, `GetAsync`, `VerifyAsync`, `RemoveAsync` |
| `ApiKeys` | `CreateAsync`, `ListAsync`, `RemoveAsync` |
| `Webhooks` | `CreateAsync`, `ListAsync`, `GetAsync`, `RemoveAsync`, `EnableAsync`, `TestAsync`, `DeliveriesAsync`, `Verify` |
| `Suppressions` | `CreateAsync`, `ListAsync`, `ListAllAsync`, `RemoveAsync` |
| `Logs` | `ListAsync`, `ListAllAsync`, `GetAsync` |
| `Usage` | `GetAsync` |

Options: `new DutaClientOptions { ApiKey, BaseUrl, MaxRetries = 2, Timeout = 30s }`.

Full documentation: https://docs.duta.indra.sh
