# AllyMailer ASP.NET Core 2.0.0

Web API 1.90

Покроковий посібник: [Надсилання листів на .NET (C#)](https://docs.allymailer.com.ua/ua/send-with-dotnet) · Документація для LLM: [llms.txt](https://docs.allymailer.com.ua/llms.txt) · [Довідник API](https://docs.allymailer.com.ua/ua/web-api-ref)

Інтеграція [.NET (C#) SDK AllyMailer](https://github.com/allymailer/allymailer-dotnet-sdk) (`AllyMailer.Sdk`) з ASP.NET Core: клієнт API в DI container (IServiceCollection), ендпоінт вебхуків із перевіркою підпису.

## Встановлення

```bash
dotnet add package AllyMailer.AspNetCore
```

Вимоги: .NET 8, ASP.NET Core 8+.

## Отримайте API-ключ

1. Зареєструйтеся на [cp.allymailer.com](https://cp.allymailer.com).
2. Скопіюйте API-ключ у [панелі керування](https://cp.allymailer.com).
3. Покладіть його у змінну оточення:

```bash
export ALLYMAILER_API_KEY="your-api-key"
```

Пакет бере його з `ALLYMAILER_API_KEY` (див. налаштування нижче).

> **Лише на сервері.** Використовуйте цей пакет у серверному коді: API-маршрути, серверні функції, воркери.
> Ніколи не передавайте API-ключ у браузер чи мобільний застосунок і не комітьте його.

## Налаштування

За замовчуванням налаштування беруться зі змінних оточення:

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

| Параметр | Змінна оточення | За замовчуванням | Опис |
|---|---|---|---|
| `AllyMailer:ApiKey` | `ALLYMAILER_API_KEY` |  | API-ключ користувача або проєкту (обов'язковий). |
| `AllyMailer:Host` | `ALLYMAILER_HOST` | `cp.allymailer.com` | Хост API (датацентр: `cp.allymailer.com`) або повний базовий URL. |
| `AllyMailer:Timeout` | `ALLYMAILER_TIMEOUT` | `30` | Таймаут запиту, секунди. |
| `AllyMailer:Webhook:Key` | `ALLYMAILER_WEBHOOK_KEY` |  | API-ключ, яким підписані вебхуки (ключ користувача/проєкту вебхука), якщо він відрізняється від API-ключа. |
| `AllyMailer:Webhook:Path` | `ALLYMAILER_WEBHOOK_PATH` | `/allymailer/webhook` | Шлях ендпоінта вебхуків. |

`AddAllyMailer()` повертає builder: `AddWebhookHandler<T>()` реєструє scoped `IAllyMailerWebhookHandler`; кожен зареєстрований обробник отримує сповіщення цілком (`HandleAsync`) і кожну його подію (`HandleEventAsync`), обидва методи мають порожні реалізації за замовчуванням. `MapAllyMailerWebhook(path)` додає ендпоінт (шлях за замовчуванням береться з налаштувань) і повертає його convention builder (`.RequireHost(...)` тощо). Транспорту пошти немає: в ASP.NET Core немає мейлера, листи надсилаються через `client.Email.SendAsync(...)`.

## Перший лист — без налаштування DNS

Кожен обліковий запис може одразу надсилати тестові листи із **sandbox-домену**: він уже підтверджений, DKIM і SPF налаштовані.
Листи доставляються лише на підтверджені вами адреси.

У прикладах — клієнт API з пакета (див. налаштування вище).

**1. Отримайте sandbox-домен** (`domain/get-sandbox-domain`). Перший виклик створює домен, наступні повертають той самий.

```csharp
using AllyMailer;

app.MapPost("/sandbox-domain", async (AllyMailerClient client) =>
{
    var response = await client.Domain.GetSandboxDomainAsync();
    return response.Domain;
});
```

**2. Підтвердіть свою скриньку** (`confirmed-email/create`). AllyMailer надішле лист із підтвердженням; перейдіть за посиланням із нього протягом 48 годин.

```csharp
using AllyMailer.Api.ConfirmedEmail.Model;
using AllyMailer;

app.MapPost("/confirm-email", async (AllyMailerClient client) =>
{
    var response = await client.ConfirmedEmail.CreateAsync(new ConfirmedEmailRequest
    {
        Email = "you@example.com",
    });
    return response.Status;
});
```

Статус перевірте через `confirmed-email/list` — у вашої адреси має бути `"status": "confirmed"`.

**3. Надішліть лист** (`email/send`) з будь-якої адреси sandbox-домену на підтверджену адресу.

```csharp
using System.Collections.Generic;
using AllyMailer.Api.Email.Model;
using AllyMailer.Common.Model;
using AllyMailer;

app.MapPost("/send", async (AllyMailerClient client) =>
{
    var response = await client.Email.SendAsync(new EmailSendRequest
    {
        Message = new MessageObject
        {
            Recipients = new List<MessageObjectRecipientsItem>
            {
                new MessageObjectRecipientsItem
                {
                    Email = "you@example.com",
                },
            },
            Body = new BodyObject
            {
                Html = "<p>Працює!</p>",
            },
            Subject = "Привіт від AllyMailer",
            FromEmail = "noreply@<sandbox-domain>",
        },
    });
    return response.JobId;
});
```

### Що повертається

```json
{
  "status": "success",
  "job_id": "1ZymBc-00041N-9X",
  "emails": [
    "you@example.com"
  ]
}
```

У разі помилки API повертає `status: "error"`, `message` і числовий `code`:

```json
{
  "status": "error",
  "code": 102,
  "message": "Error ID:83274F98-C7D5-11F1-B221-6214FF8707B1. Can not decode key wrongkey"
}
```

### Те саме через curl

```bash
BASE=https://cp.allymailer.com/ua/transactional/api/v1

curl -s -X POST $BASE/domain/get-sandbox-domain.json \
  -H "Content-Type: application/json" -H "X-API-KEY: $ALLYMAILER_API_KEY" -d '{}'

curl -s -X POST $BASE/confirmed-email/create.json \
  -H "Content-Type: application/json" -H "X-API-KEY: $ALLYMAILER_API_KEY" \
  -d '{"email": "you@example.com"}'

curl -s -X POST $BASE/email/send.json \
  -H "Content-Type: application/json" -H "X-API-KEY: $ALLYMAILER_API_KEY" \
  -d '{
        "message": {
          "recipients": [
            {
              "email": "you@example.com"
            }
          ],
          "from_email": "noreply@<sandbox-domain>",
          "subject": "Привіт від AllyMailer",
          "body": {
            "html": "<p>Працює!</p>"
          }
        }
      }'
```

## Вебхуки

Ендпоінт відповідає 200 OK на `GET` (перевірка URL під час [webhook/set](https://docs.allymailer.com.ua/ua/web-api-ref#webhook-set)) і приймає `POST`-сповіщення: перевіряє поле `auth` (MD5 тіла запиту, у якому значення `auth` замінено на API-ключ), на неправильний підпис відповідає 401, а сповіщення і кожну його подію передає застосунку.

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

Зареєструйте URL ендпоінта методом `webhook/set`, наприклад `https://example.com/allymailer/webhook`.

## Клієнт API

Усі розділи API доступні через клієнт, див. [документацію SDK](https://github.com/allymailer/allymailer-dotnet-sdk).

```csharp
using AllyMailer.Wrapper;
using AllyMailer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAllyMailer(builder.Configuration);
var app = builder.Build();

app.MapGet("/usage", async (AllyMailerClient client) =>
    (await client.System.InfoAsync()).Accounting?.EmailsSent);
```

## Надсилання через API

Клієнт викликає [email/send](https://docs.allymailer.com.ua/ua/web-api-ref#email-send) з усіма його параметрами: шаблони, підстановки, метадані, трекінг. Поля запиту та інші приклади — у [документації .NET (C#) SDK](https://github.com/allymailer/allymailer-dotnet-sdk/blob/HEAD/docs/Email.md#sendasync).

```csharp
using System.Collections.Generic;
using AllyMailer.Api.Email.Model;
using AllyMailer.Common.Model;
using AllyMailer;

app.MapPost("/send", async (AllyMailerClient client) =>
{
    var response = await client.Email.SendAsync(new EmailSendRequest
    {
        Message = new MessageObject
        {
            Recipients = new List<MessageObjectRecipientsItem>
            {
                new MessageObjectRecipientsItem
                {
                    Email = "user@example.com",
                    Substitutions = new Dictionary<string, string>
                    {
                        ["CustomerId"] = "12452",
                        ["to_name"] = "John Smith",
                    },
                },
            },
            Body = new BodyObject
            {
                Html = "<b>Hello, {{to_name}}</b>",
                Plaintext = "Hello, {{to_name}}",
            },
            Subject = "AllyMailer test email ",
            FromEmail = "email@example.com",
            FromName = "John Smith",
        },
    });
    return response.JobId;
});
```

## Обмеження sandbox

- Надсилання лише на підтверджені адреси: до 10 на обліковий запис.
- До 100 листів на день.
- Sandbox-домен спливає через 31 день; знову викличте `domain/get-sandbox-domain`, щоб отримати новий.
- Працює лише з API-ключем облікового запису, не з ключем проєкту.
- Не показується в `domain/list`; інші методи `domain/*` до нього не застосовуються.

Sandbox — для розробки та тестування. Не використовуйте sandbox-домен як відправника на проді.

## Перед продом: підтвердіть свій домен

1. `domain/get-dns-records` — повертає значення для DNS-записів.
2. Додайте ці записи у свого DNS-провайдера (їх також показує сторінка домену в панелі керування):

   | Тип | Хост | Значення |
   |---|---|---|
   | TXT | `@` | значення `verification-record` (`allymailer-validate-hash=…`) |
   | TXT | `<селектор>._domainkey` (селектор — на сторінці домену в панелі керування, наприклад `us`) | `v=DKIM1; k=rsa; p=<dkim>` |
   | TXT | `@` | `v=spf1 include:spf.allymailer.com ~all` |
   | CNAME | `_dmarc` | значення `dmarc` |

3. `domain/validate-verification-record`, потім `domain/validate-dkim`.
4. `domain/list` — дочекайтеся, поки запис підтвердження буде підтверджено, а DKIM — активний.
5. Беріть `from_email` з конфігурації, щоб перехід із sandbox не потребував змін у коді.

## Надійне надсилання

- **Повтори:** ставте `message.idempotence_key` у надсиланнях, які можуть повторюватися, щоб повторний запит не надіслав лист двічі.
- **Ліміти:** до 500 отримувачів і 10 МБ на запит; кожне вкладення до 7 МБ.
- **Тіло:** завжди передавайте `body.html` або `body.plaintext`.
- **Статус доставки:** зареєструйте вебхук (`webhook/set`) зі швидким ідемпотентним HTTPS-обробником замість опитування.
- **Типові налаштування:** не прибирайте стандартний блок відписки та трекінг; змінити їх можна через підтримку AllyMailer.
- **Помилки:** повторюйте `429` і `5xx` з backoff; інші помилки без змін не повторюйте.

## Посилання

- [Надсилання листів на .NET (C#)](https://docs.allymailer.com.ua/ua/send-with-dotnet) та інші [посібники з інтеграції](https://docs.allymailer.com.ua/ua/integrations/)
- [Довідник Web API](https://docs.allymailer.com.ua/ua/web-api-ref)
- [llms.txt](https://docs.allymailer.com.ua/llms.txt) для AI-асистентів
- [Підтримка](https://cp.allymailer.com/ua/support)

## Для розробників пакета

Див. [AGENTS.md](AGENTS.md): як цей пакет генерується з OpenAPI-специфікації, збирається, перевіряється та випускається.
