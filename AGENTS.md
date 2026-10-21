# AGENTS.md

AllyMailer ASP.NET Core — ASP.NET Core integration of the AllyMailer .NET (C#) SDK (`AllyMailer.Sdk`) for the AllyMailer transactional email Web API (Web API 1.90). How to integrate AllyMailer into a ASP.NET Core application comes first; the last section is about changing this package.

## Integrating AllyMailer with this package

ASP.NET Core integration of the [AllyMailer .NET (C#) SDK](https://github.com/allymailer/allymailer-dotnet-sdk) (`AllyMailer.Sdk`): the API client in the DI container (IServiceCollection), a webhook endpoint with signature check. Full guide: [README.md](README.md).

### Install

```bash
dotnet add package AllyMailer.AspNetCore
```

Requirements: .NET 8, ASP.NET Core 8+.

### Configuration

The API key goes to `ALLYMAILER_API_KEY`.

```json
// appsettings.json (or the environment: ALLYMAILER_API_KEY, ALLYMAILER_HOST, ...)
{
  "AllyMailer": {
    "ApiKey": "YOUR_API_KEY",
    "Host": "cp.allymailer.com",
    "Webhook": { "Path": "/allymailer/webhook" }
  }
}
```

| Option | Environment variable | Default | Description |
|---|---|---|---|
| `AllyMailer:ApiKey` | `ALLYMAILER_API_KEY` |  | User or project API key (required). |
| `AllyMailer:Host` | `ALLYMAILER_HOST` | `cp.allymailer.com` | API host (datacenter: `cp.allymailer.com`) or a full base URL. |
| `AllyMailer:Timeout` | `ALLYMAILER_TIMEOUT` | `30` | Request timeout, seconds. |
| `AllyMailer:Webhook:Key` | `ALLYMAILER_WEBHOOK_KEY` |  | API key the webhooks are signed with (the key of the user/project of the webhook), if it differs from the API key. |
| `AllyMailer:Webhook:Path` | `ALLYMAILER_WEBHOOK_PATH` | `/allymailer/webhook` | Path of the webhook endpoint. |

### API client

The client calls [email/send](https://docs.allymailer.com.ua/ua/web-api-ref#email-send) with all of its options: templates, substitutions, metadata, tracking. Fields of the request and other examples: [.NET (C#) SDK docs](https://github.com/allymailer/allymailer-dotnet-sdk/blob/HEAD/docs/Email.md#sendasync).

```csharp
using AllyMailer;
using AllyMailer.Api.Email.Model;
using AllyMailer.Common.Model;
using Recipient = AllyMailer.Api.Email.Model.MessageObjectRecipientsItem;

app.MapPost("/send", async (AllyMailerClient client) =>
{
    var response = await client.Email.SendAsync(new EmailSendRequest
    {
        Message = new MessageObject
        {
            Recipients = [Recipient.FromAddress("Alex <alex@example.com>")],
            FromEmail = "shop@example.com",
            FromName = "Shop",
            Subject = "Your order",
            Body = BodyObject.FromHtml("<p>Hello, {{to_name}}! Your order has shipped.</p>"),
        },
    });
    Console.WriteLine(response.JobId);
});
```

### Webhooks

The endpoint answers `GET` with 200 OK (the URL check of [webhook/set](https://docs.allymailer.com.ua/ua/web-api-ref#webhook-set)) and accepts the `POST` notifications: it checks the `auth` field (MD5 of the request body in which the `auth` value is replaced by the API key), answers 401 to a wrong signature, and passes the notification and each of its events to the application.

```csharp
using AllyMailer.Wrapper;
using AllyMailer.Api.Webhook.Model;

builder.Services.AddAllyMailer(builder.Configuration).AddWebhookHandler<EmailEvents>();
// ...
app.MapAllyMailerWebhook(); // /allymailer/webhook

public class EmailEvents(ILogger<EmailEvents> logger) : IAllyMailerWebhookHandler
{
    public Task HandleEventAsync(WebhookCallbackEventsByUserItemEventsItem e, WebhookCallbackEventsByUserItem user, CancellationToken cancellationToken)
    {
        logger.LogInformation("AllyMailer event {Name}: {Email}", e.EventName, e.EventData?.Email);
        return Task.CompletedTask;
    }
}
```

Register the URL of the endpoint with `webhook/set`, e.g. `https://example.com/allymailer/webhook`.

> **Answer 200 within 3 seconds** and process the events in the background (a queue): otherwise AllyMailer repeats the request every 10 minutes for 24 hours (`retry_count` grows), and a handler that failed every request for at least 10 events of a day is turned inactive. The handler must take at least **5 parallel requests of up to 100 events each**; deduplicate, a repeat delivers the same events again.

### Rules for generated code

- Server-side only. Read the API key from `ALLYMAILER_API_KEY`; never put it in browser or mobile code, never commit it.
- Base URL `https://cp.allymailer.com/ua/transactional/api/v1`; every method is POST with a JSON body; auth header `X-API-KEY`. The package does all of it.
- First email without DNS: `domain/get-sandbox-domain` → `confirmed-email/create` (the user clicks the link within 48 hours) → `email/send` from `noreply@<sandbox-domain>` to the confirmed address. Account API key only; up to 10 confirmed addresses, 100 emails a day. The domain is deleted 31 days after it was created: the next `domain/get-sandbox-domain` call creates a new one with another name, so read the name from the response, never hardcode it.
- Never ship a sandbox domain as the production sender (it is not in `domain/list`); read `from_email` from config. Production needs a verified domain: `domain/get-dns-records` → DNS records (verification TXT as is, DKIM TXT `v=DKIM1; k=rsa; p=<dkim>` on `<selector>._domainkey`, CNAME `_dmarc` → the `dmarc` field; SPF `include:spf.allymailer.com` is not in the response) → `domain/validate-verification-record`, `domain/validate-dkim` → `domain/list`.
- Set `message.idempotence_key` on retryable sends; retry network errors, `429` and `5xx` with backoff, not other errors unchanged. Up to 500 recipients and 10 MB per request with the attachments in base64 (a third larger than the files); always `body.html` or `body.plaintext`.
- Delivery events via webhook (`webhook/set`), not polling; the webhook endpoint of this package checks the `auth` signature (401 when wrong). Keep the listeners fast: answer 200 within 3 seconds and process the events from a queue, otherwise the request is repeated every 10 minutes for 24 hours; take at least 5 parallel requests of up to 100 events each; deduplicate.
- Keep the default unsubscribe and tracking behaviour; changes go through AllyMailer support.
- Do not invent methods or fields. Check the [README](README.md), the [API reference](https://docs.allymailer.com.ua/ua/web-api-ref) or [llms.txt](https://docs.allymailer.com.ua/llms.txt).

### Reference

- [AllyMailer .NET (C#) SDK](https://github.com/allymailer/allymailer-dotnet-sdk): every API method, models, errors ([AGENTS.md](https://github.com/allymailer/allymailer-dotnet-sdk/blob/HEAD/AGENTS.md))
- [API reference](https://docs.allymailer.com.ua/ua/web-api-ref)
- [Full API reference for LLMs](https://docs.allymailer.com.ua/llms-full.txt)

## Working on this repository

- **Generated code.** The whole package is generated by the AllyMailer team from the AllyMailer Web API OpenAPI spec (Web API 1.90): sources, docs, README.md and this file. Do not hand-edit it: the next release replaces the content of this repository. Report problems and requests in the issues of this repository (https://github.com/allymailer/allymailer-aspnetcore).
- **Build and check:**

  ```bash
  dotnet build -c Release
  ```

- **Source label.** Every request carries the query parameter `platform=aspnetcore.2.1.2,dotnet-sdk.2.1.2` (this package, then the SDK it is built on).
- **Tests.** Before a release the generator installs the package into a test app of ASP.NET Core and checks the client, the webhook endpoint against a mock of the API: no real API key, no real sends.
- **Release.** The version (2.1.2 in the .csproj) and the tag `v2.1.2` come from the generator; README.md states the Web API version the release was generated from.
- **Secrets.** Never commit API keys; the examples read `ALLYMAILER_API_KEY` from the environment.
