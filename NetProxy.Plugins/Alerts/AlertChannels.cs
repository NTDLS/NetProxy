using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace NetProxy.Plugins.Alerts
{
    public enum AlertSeverity { Information, Warning, Critical, Recovered }

    /// <summary>
    /// Something worth telling someone about.
    /// </summary>
    /// <param name="Key">What it is about ("endpoint:{id}"): the same thing is not told more than once in a while.</param>
    internal sealed record AlertMessage(string Key, AlertSeverity Severity, string Title, string Text, DateTime TimeUtc)
    {
        public string Plain => $"{Title}\n{Text}";
    }

    /// <summary>
    /// Somewhere to send alerts to.
    /// </summary>
    internal interface IAlertChannel
    {
        string Name { get; }

        Task SendAsync(AlertMessage alert, string source, CancellationToken cancellationToken);
    }

    internal abstract class WebChannel(HttpClient client, string url, string? authorization = null) : IAlertChannel
    {
        public abstract string Name { get; }

        protected abstract string Body(AlertMessage alert, string source);

        public async Task SendAsync(AlertMessage alert, string source, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(Body(alert, source), Encoding.UTF8, "application/json") };

            //Only for the webhook that it was set for: it is a secret, and must not go to Discord or Slack.
            if (authorization != null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            }

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode == false)
            {
                throw new InvalidOperationException($"{Name} answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }
        }

        protected static string Limit(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "...";
    }

    /// <summary>A Discord webhook (Server settings > Integrations > Webhooks).</summary>
    internal sealed class DiscordChannel(HttpClient client, string url) : WebChannel(client, url)
    {
        public override string Name => "Discord";

        protected override string Body(AlertMessage alert, string source)
            => JsonSerializer.Serialize(new { username = "NetProxy", content = Limit($"{Icon(alert.Severity)} **{alert.Title}**\n{alert.Text}\n_{source}_", 1900) });

        internal static string Icon(AlertSeverity severity) => severity switch
        {
            AlertSeverity.Recovered => "✅",
            AlertSeverity.Critical => "\U0001F6A8",
            AlertSeverity.Warning => "⚠️",
            _ => "ℹ️"
        };
    }

    /// <summary>A Slack incoming webhook.</summary>
    internal sealed class SlackChannel(HttpClient client, string url) : WebChannel(client, url)
    {
        public override string Name => "Slack";

        protected override string Body(AlertMessage alert, string source)
            => JsonSerializer.Serialize(new { text = Limit($"{DiscordChannel.Icon(alert.Severity)} *{alert.Title}*\n{alert.Text}\n_{source}_", 3500) });
    }

    /// <summary>Any URL that takes a JSON post, with an optional Authorization header.</summary>
    internal sealed class WebhookChannel(HttpClient client, string url, string? authorization) : WebChannel(client, url, authorization)
    {
        public override string Name => "Webhook";

        protected override string Body(AlertMessage alert, string source)
            => JsonSerializer.Serialize(new
            {
                source,
                severity = alert.Severity.ToString(),
                key = alert.Key,
                title = alert.Title,
                text = alert.Text,
                timeUtc = alert.TimeUtc.ToString("O")
            });
    }

    /// <summary>An e-mail through an SMTP server.</summary>
    internal sealed class SmtpChannel(SmtpSettings settings, Func<MailMessage, CancellationToken, Task>? send = null) : IAlertChannel
    {
        public string Name => "E-mail";

        public MailMessage Build(AlertMessage alert, string source)
        {
            var mail = new MailMessage { From = new MailAddress(settings.From), Subject = $"[{source}] {alert.Title}", Body = $"{alert.Text}\n\n{source}\n{alert.TimeUtc:u}" };

            foreach (var to in settings.To)
            {
                mail.To.Add(to);
            }

            return mail;
        }

        public async Task SendAsync(AlertMessage alert, string source, CancellationToken cancellationToken)
        {
            using var mail = Build(alert, source);

            if (send != null)
            {
                await send(mail, cancellationToken);
                return;
            }

            using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.Tls, Timeout = 15_000 };
            if (string.IsNullOrEmpty(settings.User) == false)
            {
                client.Credentials = new System.Net.NetworkCredential(settings.User, settings.Password);
            }

            await client.SendMailAsync(mail, cancellationToken);
        }
    }

    internal sealed record SmtpSettings(string Host, int Port, string? User, string? Password, string From, IReadOnlyList<string> To, bool Tls);
}
