namespace NetProxy.Library.Utilities
{
    public static class NpFormatters
    {
        public const long Kilobyte = 1024;
        public const long Megabyte = 1048576;
        public const long Gigabyte = 1073741824;
        public const long Terabyte = 1099511627776L;
        public const long Petabyte = 1125899906842624L;
        public const long Exabyte = 1152921504606847000L;


        public static string FormatNumeric(ulong? number, int decimals)
        {
            if (number == null)
            {
                return string.Empty;
            }
            else
            {
                return string.Format("{0:n" + decimals.ToString() + "}", (ulong)number);
            }
        }

        public static string FormatNumeric(long? number, int decimals)
        {
            if (number == null)
            {
                return string.Empty;
            }
            else
            {
                return string.Format("{0:n" + decimals.ToString() + "}", (ulong)number);
            }
        }

        public static string FormatNumeric(ulong? number)
        {
            return FormatNumeric(number, 0);
        }

        public static string FormatNumeric(long? number)
        {
            return FormatNumeric(number, 0);
        }

        public static string FormatFileSize(ulong? fileSize)
        {
            return FormatFileSize(fileSize, 2);
        }

        public static string FormatFileSize(ulong? fileSize, int decimalPlaces)
        {
            return FormatFileSize(fileSize, decimalPlaces, false);
        }

        public static string FormatFileSize(ulong? fileSize, int decimalPlaces, bool singleCharacterSuffix)
        {
            if (fileSize == null)
            {
                return string.Empty;
            }
            long divideBy = 1;
            string suffix = "";

            if (fileSize >= Exabyte)
            {
                divideBy = Exabyte;
                suffix = "EB";
            }
            else if (fileSize >= Petabyte)
            {
                divideBy = Petabyte;
                suffix = "PB";
            }
            else if (fileSize >= Terabyte)
            {
                divideBy = Terabyte;
                suffix = "TB";
            }
            else if (fileSize >= Gigabyte)
            {
                divideBy = Gigabyte;
                suffix = "GB";
            }
            else if (fileSize >= Megabyte)
            {
                divideBy = Megabyte;
                suffix = "MB";
            }
            else if (fileSize >= Kilobyte)
            {
                divideBy = Kilobyte;
                suffix = "KB";
            }
            else
            {
                divideBy = 1;
                suffix = "B";
            }

            if (singleCharacterSuffix)
            {
                suffix = suffix.Substring(0, 1);
            }

            double friendlyFileSize = (double)fileSize / divideBy;

            return friendlyFileSize.ToString("N" + decimalPlaces.ToString()) + " " + suffix;
        }
    }
}
