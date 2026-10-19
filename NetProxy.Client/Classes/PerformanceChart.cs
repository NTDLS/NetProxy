using System.Windows.Forms.DataVisualization.Charting;

namespace NetProxy.Client.Classes
{
    /// <summary>
    /// The chart on the main window: throughput to and from clients as two areas (left axis, in B/s, KB/s, MB/s... as the numbers
    /// need) and the number of connections as a line (right axis), over the last few minutes.
    /// </summary>
    public class PerformanceChart
    {
        private const string AreaName = "Performance";
        private const string SentName = "Sent";
        private const string ReceivedName = "Received";
        private const string ConnectionsName = "Connections";

        //Colors that are told apart by people who do not see red and green, and that work on a light and on a dark background.
        private static readonly Color _sentColor = Color.FromArgb(0xF5, 0x9E, 0x0B);
        private static readonly Color _receivedColor = Color.FromArgb(0x3B, 0x82, 0xF6);
        private static readonly Color _connectionsColor = Color.FromArgb(0xA8, 0x55, 0xF7);

        private readonly Chart _chart;
        private readonly Title _title;

        public PerformanceChart(Chart chart)
        {
            _chart = chart;

            var text = SystemColors.ControlText;
            var gridColor = Color.FromArgb(50, text);

            _chart.Series.Clear();
            _chart.ChartAreas.Clear();
            _chart.Legends.Clear();
            _chart.Titles.Clear();

            _chart.BackColor = SystemColors.Control;
            _chart.BorderlineWidth = 0;
            _chart.AntiAliasing = AntiAliasingStyles.All;
            _chart.TextAntiAliasingQuality = TextAntiAliasingQuality.High;

            var area = new ChartArea(AreaName)
            {
                BackColor = SystemColors.Window,
                BorderColor = gridColor,
                BorderDashStyle = ChartDashStyle.Solid
            };

            ConfigureAxis(area.AxisX, text, gridColor);
            area.AxisX.LabelStyle.Format = "HH:mm:ss";
            area.AxisX.IntervalType = DateTimeIntervalType.Seconds;
            area.AxisX.Interval = 60;
            area.AxisX.MajorTickMark.Enabled = false;

            ConfigureAxis(area.AxisY, text, gridColor);
            area.AxisY.Minimum = 0;
            area.AxisY.IsStartedFromZero = true;
            area.AxisY.MajorGrid.Enabled = true;

            ConfigureAxis(area.AxisY2, text, gridColor);
            area.AxisY2.Minimum = 0;
            area.AxisY2.IsStartedFromZero = true;
            area.AxisY2.MajorGrid.Enabled = false;
            area.AxisY2.Enabled = AxisEnabled.True;
            area.AxisY2.LabelStyle.ForeColor = _connectionsColor;

            _chart.ChartAreas.Add(area);

            var legend = new Legend("Legend")
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Near,
                MaximumAutoSize = 100, //The default is half of the chart, which wraps the legend.
                BackColor = Color.Transparent,
                ForeColor = text,
                LegendStyle = LegendStyle.Row
            };
            _chart.Legends.Add(legend);

            AddSeries(SentName, SeriesChartType.SplineArea, _sentColor, AxisType.Primary, alpha: 110);
            AddSeries(ReceivedName, SeriesChartType.SplineArea, _receivedColor, AxisType.Primary, alpha: 110);
            AddSeries(ConnectionsName, SeriesChartType.StepLine, _connectionsColor, AxisType.Secondary, alpha: 255);
            _chart.Series[ConnectionsName].BorderWidth = 2;

            _title = new Title(string.Empty, Docking.Top, new Font(SystemFonts.DefaultFont, FontStyle.Bold), text)
            {
                Alignment = ContentAlignment.TopLeft
            };
            _chart.Titles.Add(_title);

            //Numbers on the throughput axis are rates, in whatever unit suits their size.
            _chart.FormatNumber += (sender, e) =>
            {
                if (e.ElementType == ChartElementType.AxisLabels && ReferenceEquals(sender, _chart) == false && sender is Axis axis
                    && axis.AxisName == AxisName.Y)
                {
                    e.LocalizedValue = Formatting.Rate(e.Value);
                }
            };

            _chart.GetToolTipText += Chart_GetToolTipText;
        }

        private static void ConfigureAxis(Axis axis, Color text, Color gridColor)
        {
            axis.LabelStyle.ForeColor = text;
            axis.LineColor = gridColor;
            axis.MajorGrid.LineColor = gridColor;
            axis.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            axis.MajorTickMark.LineColor = gridColor;
        }

        private void AddSeries(string name, SeriesChartType type, Color color, AxisType axis, int alpha)
        {
            _chart.Series.Add(new Series(name)
            {
                ChartArea = AreaName,
                Legend = "Legend",
                ChartType = type,
                Color = Color.FromArgb(alpha, color),
                BorderColor = color,
                BorderWidth = 1,
                YAxisType = axis,
                XValueType = ChartValueType.DateTime,
                IsXValueIndexed = false
            });
        }

        /// <summary>
        /// Shows the history of one proxy (or of all of them, with <see cref="PerformanceHistory.All"/>).
        /// </summary>
        public void Render(IReadOnlyList<PerformanceSample> samples, string title, DateTime now)
        {
            _title.Text = title;

            var sent = _chart.Series[SentName];
            var received = _chart.Series[ReceivedName];
            var connections = _chart.Series[ConnectionsName];

            sent.Points.Clear();
            received.Points.Clear();
            connections.Points.Clear();

            foreach (var sample in samples)
            {
                var x = sample.Time.ToOADate();
                sent.Points.AddXY(x, sample.SentPerSecond);
                received.Points.AddXY(x, sample.ReceivedPerSecond);
                connections.Points.AddXY(x, sample.Connections);
            }

            var last = samples.Count > 0 ? samples[^1] : default;
            sent.LegendText = Localize("Sent {0}", Formatting.Rate(last.SentPerSecond));
            received.LegendText = Localize("Received {0}", Formatting.Rate(last.ReceivedPerSecond));
            connections.LegendText = Localize("Connections {0}", Formatting.Count(last.Connections));

            var area = _chart.ChartAreas[AreaName];
            area.AxisX.Minimum = (now - PerformanceHistory.Window).ToOADate();
            area.AxisX.Maximum = now.ToOADate();

            ScaleRateAxis(area.AxisY, samples.Count > 0 ? Math.Max(samples.Max(o => o.SentPerSecond), samples.Max(o => o.ReceivedPerSecond)) : 0);

            //A few connections should not look like a lot of them: the axis shows whole numbers and at least 0 to 4.
            var maxConnections = samples.Count > 0 ? samples.Max(o => o.Connections) : 0;
            area.AxisY2.Maximum = Math.Max(4, Math.Ceiling(maxConnections * 1.2));
            area.AxisY2.Interval = Math.Max(1, Math.Ceiling(area.AxisY2.Maximum / 4));
        }

        /// <summary>
        /// Sets the throughput axis to a maximum and steps that read well in the unit the numbers are in (1 KB/s, 2, 4... rather
        /// than 488.28), since the numbers on this axis are binary (1024) multiples.
        /// </summary>
        private static void ScaleRateAxis(Axis axis, double maxRate)
        {
            double unit = 1;
            maxRate = Math.Max(maxRate, 1024); //An idle proxy shows a small axis rather than an empty one.

            while (maxRate / unit >= 1024 && unit < Math.Pow(1024, 5))
            {
                unit *= 1024;
            }

            var scaled = maxRate / unit;
            var rough = scaled / 4;
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            var normalized = rough / magnitude;
            var step = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * magnitude;

            axis.Interval = step * unit;
            axis.Maximum = Math.Ceiling(scaled / step) * step * unit;
        }


        private void Chart_GetToolTipText(object? sender, ToolTipEventArgs e)
        {
            if (e.HitTestResult.ChartElementType != ChartElementType.DataPoint || e.HitTestResult.Series == null)
            {
                return;
            }

            var index = e.HitTestResult.PointIndex;
            var series = e.HitTestResult.Series;
            if (index < 0 || index >= series.Points.Count)
            {
                return;
            }

            var time = DateTime.FromOADate(series.Points[index].XValue);
            var sent = _chart.Series[SentName].Points[index].YValues[0];
            var received = _chart.Series[ReceivedName].Points[index].YValues[0];
            var connections = _chart.Series[ConnectionsName].Points[index].YValues[0];

            e.Text = $"{time:HH:mm:ss}\n" + Localize("Sent to clients: {0}", Formatting.Rate(sent))
                + "\n" + Localize("Received from clients: {0}", Formatting.Rate(received))
                + "\n" + Localize("Connections: {0}", Formatting.Count(connections));
        }
    }
}
