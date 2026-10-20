using System;
using System.Globalization;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using AllyMailer.Client;

namespace AllyMailer.Wrapper;

/// <summary>Registration of the AllyMailer API client.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AllyMailer.AllyMailerClient"/> as a singleton, configured from the section "AllyMailer" of the
    /// configuration and the ALLYMAILER_* environment variables.
    /// </summary>
    public static AllyMailerBuilder AddAllyMailer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AllyMailerOptions>().Bind(configuration.GetSection(AllyMailerOptions.Section));
        return services.AddAllyMailer(_ => { });
    }

    /// <summary>Registers <see cref="AllyMailer.AllyMailerClient"/> as a singleton configured in code.</summary>
    public static AllyMailerBuilder AddAllyMailer(this IServiceCollection services, Action<AllyMailerOptions> configure)
    {
        services.AddOptions<AllyMailerOptions>().Configure(configure).PostConfigure(FromEnvironment);
        services.TryAddSingleton(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<AllyMailerOptions>>().Value;
            if (string.IsNullOrEmpty(options.ApiKey))
            {
                throw new InvalidOperationException("AllyMailer: set AllyMailer:ApiKey in the configuration or ALLYMAILER_API_KEY.");
            }
            var timeout = TimeSpan.FromSeconds(options.Timeout);
            var config = new Configuration(options.ApiKey)
            {
                BaseUrl = string.IsNullOrEmpty(options.Host) ? Configuration.DefaultBaseUrl : Configuration.BaseUrlFor(options.Host),
                Timeout = timeout,
                Platform = "aspnetcore.2.0.0",
            };
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = timeout,
            };
            return new AllyMailer.AllyMailerClient(config, http);
        });
        return new AllyMailerBuilder(services);
    }

    private static void FromEnvironment(AllyMailerOptions options)
    {
        static string? Env(string name)
        {
            var value = Environment.GetEnvironmentVariable("ALLYMAILER_" + name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            options.ApiKey = Env("API_KEY");
        }
        if (string.IsNullOrEmpty(options.Host))
        {
            options.Host = Env("HOST");
        }
        if (Env("TIMEOUT") is { } timeout && double.TryParse(timeout, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            options.Timeout = seconds;
        }
        if (string.IsNullOrEmpty(options.Webhook.Key))
        {
            options.Webhook.Key = Env("WEBHOOK_KEY");
        }
        if (Env("WEBHOOK_PATH") is { } path)
        {
            options.Webhook.Path = path;
        }
    }
}

/// <summary>Result of <c>AddAllyMailer()</c>: registers the webhook handlers.</summary>
public class AllyMailerBuilder
{
    public AllyMailerBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    /// <summary>Registers a scoped webhook handler; every registered handler gets each notification.</summary>
    public AllyMailerBuilder AddWebhookHandler<T>() where T : class, IAllyMailerWebhookHandler
    {
        Services.AddScoped<IAllyMailerWebhookHandler, T>();
        return this;
    }
}
