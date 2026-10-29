using System.Globalization;
using System.Text;

namespace NetProxy.Plugins.Metrics
{
    /// <summary>
    /// Counters, gauges and histograms, written out in the text format that Prometheus reads (and that Grafana Agent, Telegraf, VictoriaMetrics
    /// and others read too).
    /// </summary>
    internal sealed class MetricsRegistry
    {
        private sealed class Histogram(double[] bounds)
        {
            public readonly double[] Bounds = bounds;
            public readonly long[] Counts = new long[bounds.Length];
            public long Count;
            public double Sum;
        }

        private readonly object _lock = new();
        private readonly Dictionary<string, (string Type, string Help)> _declared = [];
        private readonly Dictionary<(string Name, string Labels), double> _values = [];
        private readonly Dictionary<(string Name, string Labels), Histogram> _histograms = [];

        public void Declare(string name, string type, string help)
        {
            lock (_lock)
            {
                _declared[name] = (type, help);
            }
        }

        public void Add(string name, string labels, double delta = 1)
        {
            lock (_lock)
            {
                _values[(name, labels)] = _values.GetValueOrDefault((name, labels)) + delta;
            }
        }

        public void Set(string name, string labels, double value)
        {
            lock (_lock)
            {
                _values[(name, labels)] = value;
            }
        }

        public double Get(string name, string labels)
        {
            lock (_lock)
            {
                return _values.GetValueOrDefault((name, labels));
            }
        }

        public void Observe(string name, string labels, double value, double[] bounds)
        {
            lock (_lock)
            {
                if (_histograms.TryGetValue((name, labels), out var histogram) == false)
                {
                    histogram = new Histogram(bounds);
                    _histograms[(name, labels)] = histogram;
                }

                for (var i = 0; i < histogram.Bounds.Length; i++)
                {
                    if (value <= histogram.Bounds[i])
                    {
                        histogram.Counts[i]++;
                    }
                }

                histogram.Count++;
                histogram.Sum += value;
            }
        }

        /// <summary>
        /// Labels of a sample: <c>{proxy="web",code="2xx"}</c>, with the values made safe.
        /// </summary>
        public static string Labels(params (string Name, string Value)[] labels)
            => labels.Length == 0 ? string.Empty : "{" + string.Join(",", labels.Select(o => $"{o.Name}=\"{Escape(o.Value)}\"")) + "}";

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", string.Empty);

        public string Render()
        {
            var text = new StringBuilder();

            lock (_lock)
            {
                var names = _values.Keys.Select(o => o.Name).Concat(_histograms.Keys.Select(o => o.Name)).Distinct().Order(StringComparer.Ordinal);

                foreach (var name in names)
                {
                    if (_declared.TryGetValue(name, out var meta))
                    {
                        text.Append("# HELP ").Append(name).Append(' ').Append(meta.Help.Replace("\n", " ")).Append('\n');
                        text.Append("# TYPE ").Append(name).Append(' ').Append(meta.Type).Append('\n');
                    }

                    foreach (var sample in _values.Where(o => o.Key.Name == name).OrderBy(o => o.Key.Labels, StringComparer.Ordinal))
                    {
                        text.Append(name).Append(sample.Key.Labels).Append(' ').Append(Number(sample.Value)).Append('\n');
                    }

                    foreach (var sample in _histograms.Where(o => o.Key.Name == name).OrderBy(o => o.Key.Labels, StringComparer.Ordinal))
                    {
                        var inner = sample.Key.Labels.Length > 0 ? sample.Key.Labels[1..^1] + "," : string.Empty;

                        for (var i = 0; i < sample.Value.Bounds.Length; i++)
                        {
                            text.Append(name).Append("_bucket{").Append(inner).Append("le=\"").Append(Number(sample.Value.Bounds[i])).Append("\"} ").Append(sample.Value.Counts[i]).Append('\n');
                        }

                        text.Append(name).Append("_bucket{").Append(inner).Append("le=\"+Inf\"} ").Append(sample.Value.Count).Append('\n');
                        text.Append(name).Append("_sum").Append(sample.Key.Labels).Append(' ').Append(Number(sample.Value.Sum)).Append('\n');
                        text.Append(name).Append("_count").Append(sample.Key.Labels).Append(' ').Append(sample.Value.Count).Append('\n');
                    }
                }
            }

            return text.ToString();
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
