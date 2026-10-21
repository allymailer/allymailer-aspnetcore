# AllyMailer ASP.NET Core

[![ASP.NET Core](https://img.shields.io/nuget/v/AllyMailer.AspNetCore)](https://www.nuget.org/packages/AllyMailer.AspNetCore) [![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

Інтеграція [.NET (C#) SDK AllyMailer](https://github.com/allymailer/allymailer-dotnet-sdk) (`AllyMailer.Sdk`) з ASP.NET Core: клієнт API в DI container (IServiceCollection), ендпоінт вебхуків із перевіркою підпису.

[Довідник API](https://docs.allymailer.com.ua/ua/web-api-ref) · Документація для LLM: [llms.txt](https://docs.allymailer.com.ua/llms.txt)

## Встановлення

```bash
dotnet add package AllyMailer.AspNetCore
```

Вимоги: .NET 8, ASP.NET Core 8+.

## Налаштування

Отримайте API-ключ у [панелі керування](https://cp.allymailer.com) і зберігайте його у змінній оточення, а не в коді:

```bash
export ALLYMAILER_API_KEY="your-api-key"
```

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

> **Відповідайте 200 протягом 3 секунд**, а події обробляйте у фоні (через чергу): інакше AllyMailer повторює запит кожні 10 хвилин протягом доби (зростає `retry_count`), а обробник, на якому за добу не пройшов жоден запит хоча б для 10 подій, вимикається. Обробник має витримувати **щонайменше 5 паралельних запитів до 100 подій у кожному**; повтор надсилає ті самі події ще раз — прибирайте дублікати.

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

### Надсилання через API

Клієнт викликає [email/send](https://docs.allymailer.com.ua/ua/web-api-ref#email-send) з усіма його параметрами: шаблони, підстановки, метадані, трекінг. Поля запиту та інші приклади — у [документації .NET (C#) SDK](https://github.com/allymailer/allymailer-dotnet-sdk/blob/HEAD/docs/Email.md#sendasync).

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
            Recipients = [Recipient.FromAddress("Олексій <alex@example.com>")],
            FromEmail = "shop@example.com",
            FromName = "Магазин",
            Subject = "Ваше замовлення",
            Body = BodyObject.FromHtml("<p>Вітаємо, {{to_name}}! Ваше замовлення надіслано.</p>"),
        },
    });
    Console.WriteLine(response.JobId);
});
```

## Без свого домену: тестовий sandbox-домен

Поки ваш домен не підтверджено, листи можна надсилати із **sandbox-домену** — тестового домену, який є в кожного облікового запису: він уже підтверджений, DKIM і SPF налаштовані. Листи з нього доставляються лише на підтверджені вами адреси (посилання для підтвердження приходить листом і діє 48 годин), до 100 на день. Через 31 день після створення sandbox-домен видаляється, але його можна отримати знову: наступний виклик `domain/get-sandbox-domain` створить новий (з іншою назвою, тому беріть її з відповіді, а не прописуйте в коді).

У прикладах — клієнт API з пакета (див. налаштування вище).

```csharp
using AllyMailer;
using AllyMailer.Api.ConfirmedEmail.Model;
using AllyMailer.Api.Email.Model;
using AllyMailer.Common.Model;
using Recipient = AllyMailer.Api.Email.Model.MessageObjectRecipientsItem;

app.MapPost("/sandbox", async (AllyMailerClient client) =>
{
    // 1. Ваш sandbox-домен: створюється під час першого виклику, далі повертається той самий
    var sandbox = await client.Domain.GetSandboxDomainAsync();

    // 2. Підтвердіть свою скриньку: AllyMailer надішле посилання, перейдіть за ним протягом 48 годин
    await client.ConfirmedEmail.CreateAsync(new ConfirmedEmailRequest { Email = "you@example.com" });

    // 3. Надішліть із будь-якої адреси sandbox-домену на підтверджену адресу
    await client.Email.SendAsync(new EmailSendRequest
    {
        Message = new MessageObject
        {
            Recipients = [Recipient.FromAddress("you@example.com")],
            FromEmail = $"noreply@{sandbox.Domain}",
            Subject = "Привіт від AllyMailer",
            Body = BodyObject.FromHtml("<p>Працює!</p>"),
        },
    });
});
```

<details>
<summary>Обмеження sandbox</summary>

- Надсилання лише на підтверджені адреси: до 10 на обліковий запис.
- До 100 листів на день.
- Sandbox-домен спливає через 31 день; знову викличте `domain/get-sandbox-domain`, щоб отримати новий.
- Працює лише з API-ключем облікового запису, не з ключем проєкту.
- Не показується в `domain/list`; інші методи `domain/*` до нього не застосовуються.

Sandbox — для розробки та тестування. Не використовуйте sandbox-домен як відправника на проді.

</details>

<details>
<summary>Те саме через curl</summary>

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

</details>

## Запуск у продакшен

**1. Підтвердіть домен надсилання.** `domain/get-dns-records` повертає запис підтвердження, DKIM-ключ і адресу для DMARC; додайте записи у свого DNS-провайдера, потім `domain/validate-verification-record` і `domain/validate-dkim`.

| Тип | Хост | Значення | Звідки взяти |
|---|---|---|---|
| TXT | `@` | `allymailer-validate-hash=…` | поле `verification-record` відповіді, як є |
| TXT | `<селектор>._domainkey` (селектор — на сторінці домену в панелі керування, наприклад `us`) | `v=DKIM1; k=rsa; p=<dkim>` | поле `dkim` відповіді після `v=DKIM1; k=rsa; p=` |
| TXT | `@` | `v=spf1 include:spf.allymailer.com ~all` | у відповіді немає; якщо SPF-запис уже є, додайте include до нього |
| CNAME | `_dmarc` | `<домен>.dmarc.…` | поле `dmarc` відповіді (DMARC-політику публікує AllyMailer); якщо в домену вже є свій TXT-запис DMARC, залиште його |

**2. Надійне надсилання.**

| | |
|---|---|
| Повтори | `429` і `5xx` з backoff і тим самим `idempotence_key`; інші помилки виправляйте, а не повторюйте |
| Ліміти | 500 одержувачів і 10 МБ на запит разом із вкладеннями: у base64 файл стає приблизно на третину більшим |
| Тіло | завжди `html` або `plaintext` (або із шаблону) |
| Статус доставки | вебхук, а не опитування |
| Черга | надсилайте з фонового завдання, а не всередині веб-запиту |
| Налаштування | `from_email` з конфігурації: перехід із sandbox на продакшен без змін у коді |
| Типові налаштування | не прибирайте блок відписки та трекінг; змінити їх можна через підтримку AllyMailer |

## Допомога

- [.NET (C#) SDK](https://github.com/allymailer/allymailer-dotnet-sdk)
- Для ШІ-агентів: [llms.txt](https://docs.allymailer.com.ua/llms.txt), скіли: `npx skills add https://github.com/allymailer/allymailer-skills.git`
- [Підтримка](https://cp.allymailer.com/ua/support) · [Задачі](https://github.com/allymailer/allymailer-aspnetcore/issues)
- Розробникам: [AGENTS.md](AGENTS.md) — пакет генерується з OpenAPI-специфікації.
