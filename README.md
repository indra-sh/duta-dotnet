# Duta .NET SDK

Official .NET client for [Duta](https://duta.indra.sh). Targets .NET 8.

## Install

```bash
dotnet add package Duta.Net
```

## Quickstart

```csharp
using Duta;

var duta = new DutaClient("duta_live_xxx");

var result = await duta.Emails.SendAsync(new SendEmailOptions
{
    From = "hello@yourdomain.com",
    To = new[] { "user@example.com" },
    Subject = "Welcome to Duta",
    Html = "<p>Thanks for signing up!</p>",
});

Console.WriteLine($"Sent: {result.Id}");
```

Get an API key from the [dashboard](https://app.duta.indra.sh). The sender domain must be verified first.

## Error handling

Methods throw `DutaException` on failure:

```csharp
try
{
    await duta.Emails.SendAsync(options);
}
catch (DutaException e)
{
    Console.WriteLine($"{e.StatusCode} {e.Name}: {e.Message}");
    // e.Name: authentication_error | permission_denied | rate_limit_exceeded | ...
}
```

## API

### `new DutaClient(string apiKey, string baseUrl = ..., HttpClient? httpClient = null)`

Pass your own `HttpClient` to reuse a shared instance (recommended in long-running apps).

### `await duta.Emails.SendAsync(SendEmailOptions options)`

Returns `SendEmailResult` with `Id` and `Status`.

### `await duta.Emails.GetAsync(string id)`

Retrieve one email. Requires a full-access API key.

### `await duta.Emails.ListAsync(int page = 1, int limit = 20)`

List emails, newest first. Requires a full-access API key.

## License

MIT
