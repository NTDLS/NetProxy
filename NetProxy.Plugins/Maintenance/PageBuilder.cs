using System.Net;
using System.Text;

namespace NetProxy.Plugins.Maintenance
{
    /// <summary>
    /// Makes the response that the client is answered with: the status line, the headers and the page. The page is the PageFile when
    /// there is one (read again when the file changes), and otherwise one that is built from the title and the message.
    /// </summary>
    internal sealed class PageBuilder(MaintenanceSettings settings, string dataDirectory, TimeProvider clock)
    {
        private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(2);

        private readonly object _lock = new();
        private byte[]? _file;
        private DateTime _fileStamp;
        private long _fileLength = -1;
        private DateTime _checkedUtc;
        private string? _problem;

        /// <summary>Why the PageFile could not be used, or null. The built page is used instead.</summary>
        public string? Problem
        {
            get { lock (_lock) { return _problem; } }
        }

        /// <param name="retryAfterSeconds">When to try again, if that is known (the end of a planned window). Otherwise the setting is used.</param>
        public byte[] Response(int? retryAfterSeconds = null)
        {
            var body = PageFileBytes() ?? Encoding.UTF8.GetBytes(DefaultPage());
            var head = new StringBuilder();

            head.Append("HTTP/1.1 ").Append(settings.StatusCode).Append(' ').Append(Reason(settings.StatusCode)).Append("\r\n");
            head.Append("Content-Type: ").Append(settings.ContentType).Append("\r\n");
            head.Append("Content-Length: ").Append(body.Length).Append("\r\n");

            var retryAfter = retryAfterSeconds ?? settings.RetryAfterSeconds;
            if (retryAfter > 0)
            {
                head.Append("Retry-After: ").Append(retryAfter).Append("\r\n");
            }

            //A page that says "down for maintenance" must not be kept by a browser or a cache after the site is back.
            head.Append("Cache-Control: no-store\r\n");
            head.Append("Connection: close\r\n\r\n");

            return [.. Encoding.ASCII.GetBytes(head.ToString()), .. body];
        }

        private byte[]? PageFileBytes()
        {
            if (settings.PageFile == null)
            {
                return null;
            }

            lock (_lock)
            {
                var now = clock.GetUtcNow().UtcDateTime;
                if (_checkedUtc != default && now - _checkedUtc < CheckEvery)
                {
                    return _file;
                }

                _checkedUtc = now;

                try
                {
                    var info = new FileInfo(Path.GetFullPath(settings.PageFile, dataDirectory));

                    if (info.Exists == false)
                    {
                        _file = null;
                        _problem = $"The page file '{info.FullName}' does not exist, so the built page is used.";
                    }
                    else if (_file == null || info.LastWriteTimeUtc != _fileStamp || info.Length != _fileLength)
                    {
                        _file = File.ReadAllBytes(info.FullName);
                        _fileStamp = info.LastWriteTimeUtc;
                        _fileLength = info.Length;
                        _problem = null;
                    }
                }
                catch (Exception ex)
                {
                    //A file that is being saved, or cannot be read: the page that was read before stays, or the built one is used.
                    _problem ??= $"The page file could not be read, so the built page is used: {ex.GetBaseException().Message}";
                }

                return _file;
            }
        }

        private string DefaultPage()
        {
            var title = WebUtility.HtmlEncode(settings.Title);
            var message = WebUtility.HtmlEncode(settings.Message).Replace("\r\n", "\n").Replace("\n", "<br>");

            return $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="robots" content="noindex">
                <title>{{title}}</title>
                <style>
                :root { color-scheme: light dark; --bg: #f5f6f8; --fg: #1d2330; --muted: #5d6677; --accent: #2f6fed; }
                @media (prefers-color-scheme: dark) { :root { --bg: #14171f; --fg: #e8ebf2; --muted: #9aa3b5; --accent: #6b9bff; } }
                html, body { height: 100%; margin: 0; }
                body { display: flex; align-items: center; justify-content: center; background: var(--bg); color: var(--fg);
                       font: 16px/1.5 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; padding: 0 16px; }
                main { max-width: 32rem; text-align: center; }
                .bar { width: 3rem; height: 4px; border-radius: 2px; background: var(--accent); margin: 0 auto 1.5rem; }
                h1 { font-size: 1.75rem; margin: 0 0 .75rem; }
                p { margin: 0; color: var(--muted); }
                </style>
                </head>
                <body>
                <main>
                <div class="bar"></div>
                <h1>{{title}}</h1>
                <p>{{message}}</p>
                </main>
                </body>
                </html>
                """;
        }

        private static string Reason(int status)
            => status switch
            {
                500 => "Internal Server Error",
                502 => "Bad Gateway",
                503 => "Service Unavailable",
                504 => "Gateway Timeout",
                _ => Enum.IsDefined(typeof(HttpStatusCode), status)
                    ? string.Concat(((HttpStatusCode)status).ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()))
                    : "Unavailable"
            };
    }
}
