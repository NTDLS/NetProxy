using System.Globalization;

namespace NetProxy.Client.Classes
{
    public static class Formatting
    {
        private static readonly string[] _byteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

        /// <summary>
        /// A number of bytes as "512 B", "1.5 KB", "3.2 GB"...
        /// </summary>
        public static string Bytes(double bytes)
        {
            var value = Math.Max(0, bytes);
            int unit = 0;

            while (value >= 1024 && unit < _byteUnits.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0
                ? string.Create(CultureInfo.CurrentCulture, $"{value:0} {_byteUnits[unit]}")
                : string.Create(CultureInfo.CurrentCulture, $"{value:0.##} {_byteUnits[unit]}");
        }

        /// <summary>
        /// A rate as "512 B/s", "1.5 MB/s"...
        /// </summary>
        public static string Rate(double bytesPerSecond) => Bytes(bytesPerSecond) + "/s";

        public static string Count(double count) => string.Create(CultureInfo.CurrentCulture, $"{count:n0}");
    }
}
