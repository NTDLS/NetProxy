using Newtonsoft.Json;

namespace NetProxy.Cli
{
    /// <summary>
    /// What the tool prints: tables for people, JSON for scripts.
    /// </summary>
    internal static class Output
    {
        public static void Json(object value)
            => Console.WriteLine(JsonConvert.SerializeObject(value, Formatting.Indented));

        public static void Table(string[] headers, IEnumerable<string[]> rows)
        {
            var all = rows.ToList();
            var widths = headers.Select((h, i) => Math.Max(h.Length, all.Select(r => r[i].Length).DefaultIfEmpty(0).Max())).ToArray();

            Console.WriteLine(string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i]))).TrimEnd());
            foreach (var row in all)
            {
                Console.WriteLine(string.Join("  ", row.Select((c, i) => c.PadRight(widths[i]))).TrimEnd());
            }
        }

        public static string Bytes(ulong bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double value = bytes;
            var unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
        }
    }
}

namespace NetProxy.Cli
{
    /// <summary>
    /// The standard input. Windows PowerShell puts a byte order mark in front of what it pipes in, which would end up in a password
    /// or in the first property name of a file.
    /// </summary>
    internal static class Input
    {
        public static string? ReadLine() => Console.In.ReadLine()?.TrimStart('﻿');

        public static string ReadAll() => Console.In.ReadToEnd().TrimStart('﻿');
    }
}
