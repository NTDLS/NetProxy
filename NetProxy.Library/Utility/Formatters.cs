namespace NetProxy.Library.Utility
{
    public static class Formatters
    {
        #region Constants.

        public const long Kilobyte = 1024;
        public const long Megabyte = 1048576;
        public const long Gigabyte = 1073741824;
        public const long Terabyte = 1099511627776L;
        public const long Petabyte = 1125899906842624L;
        public const long Exabyte = 1152921504606847000L;

        public const long Megahertz = 1000000L;
        public const long Gigahertz = 1000000000L;
        public const long Terahertz = 1000000000000L;

        public const long Kilobit = 1000L;
        public const long Megabit = 1000000L;
        public const long Gigibit = 1000000000L;
        public const long Terabit = 1000000000000L;

        // Used for the charting to abbreviate axis label values
        public const long Thousand = 1000; // Thousand
        public const long Million = Thousand * 1000; // Million
        public const long Billion = Million * 1000; // Billion
        public const long Trillion = Billion * 1000; // Trillion

        #endregion

        public static string TrimTextWithElipses(string text, int maxLength)
        {
            string cleanedText = Strings.RemoveLineBreaksAndExtraSpaces(text);
            if (cleanedText != null && cleanedText.Length > maxLength)
            {
                return cleanedText.Substring(0, maxLength) + "...";
            }
            else
            {
                return cleanedText;
            }
        }

        public static string CleanControlName(string nameString)
        {
            return Strings.RemoveSpecialCharacters(nameString).Replace("_", string.Empty).Replace(".", string.Empty);
        }

        public static string FormatAs1St2Nd3Rd(int number)
        {
            string stringNumber = number.ToString("#,0");

            int absNumber = Math.Abs(number);
            if (absNumber == 1)
            {
                stringNumber += "st";
            }
            else if (absNumber == 2)
            {
                stringNumber += "nd";
            }
            else if (absNumber == 3)
            {
                stringNumber += "rd";
            }
            else
            {
                stringNumber += "th";
            }

            return stringNumber;
        }

        public static string FormatNumberToAbbreviation(string numberString)
        {
            const string trillionSuffix = "T";
            const string billionSuffix = "B";
            const string millionSuffix = "M";
            const string thousandSuffix = "K";

            decimal number = 0.0m;
            int decimalPlaces = 2;
            if (decimal.TryParse(numberString, System.Globalization.NumberStyles.Any, null, out number) == false)
            {
                return numberString;
            }
            else
            {
                long divideBy = 1;
                string suffix = "";
                bool negative = false;

                if (number < 0)
                {
                    negative = true;
                    number *= -1;
                }

                if (number >= Trillion)
                {
                    divideBy = Trillion;
                    suffix = trillionSuffix;
                }
                else if (number >= Billion)
                {
                    divideBy = Billion;
                    suffix = billionSuffix;
                }
                else if (number >= Million)
                {
                    divideBy = Million;
                    suffix = millionSuffix;
                }
                else if (number >= Thousand)
                {
                    divideBy = Thousand;
                    suffix = thousandSuffix;
                }
                else
                {
                    divideBy = 1;
                    suffix = string.Empty;
                }

                double friendlyNumber = ((double)number) / ((double)divideBy);

                if (negative)
                {
                    friendlyNumber *= -1;
                }

                return friendlyNumber.ToString("N" + decimalPlaces.ToString()) + suffix;
            }
        }

        public static string FormatNumeric(ulong? number, int decimals)
        {
            if (number == null)
            {
                return string.Empty;
            }
            else
            {
                return String.Format("{0:n" + decimals.ToString() + "}", (ulong)number);
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
                return String.Format("{0:n" + decimals.ToString() + "}", (ulong)number);
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

        public static string FormatFileSize(long? fileSize)
        {
            return FormatFileSize(fileSize, 2);
        }

        public static string FormatFileSize(long? fileSize, int decimalPlaces)
        {
            return FormatFileSize(fileSize, decimalPlaces, false);
        }

        public static string FormatFileSize(long? fileSize, int decimalPlaces, bool singleCharacterSuffix)
        {
            if (fileSize == null)
            {
                return string.Empty;

            }
            long divideBy = 1;
            string suffix = "";
            bool negative = false;

            if (fileSize < 0)
            {
                negative = true;
                fileSize *= -1;
            }

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

            double friendlyFileSize = ((double)fileSize) / ((double)divideBy);

            if (negative)
            {
                friendlyFileSize *= -1;
            }

            return friendlyFileSize.ToString("N" + decimalPlaces.ToString()) + " " + suffix;
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

            double friendlyFileSize = ((double)fileSize) / ((double)divideBy);

            return friendlyFileSize.ToString("N" + decimalPlaces.ToString()) + " " + suffix;
        }

        public static string FormatFileSize(decimal? fileSize)
        {
            return FormatFileSize(fileSize, 2);
        }

        public static string FormatFileSize(decimal? fileSize, int decimalPlaces)
        {
            return FormatFileSize(fileSize, decimalPlaces, false);
        }

        public static string FormatFileSize(decimal? fileSize, int decimalPlaces, bool singleCharacterSuffix)
        {
            if (fileSize == null)
            {
                return string.Empty;
            }
            decimal divideBy = 1;
            string suffix = "";
            bool negative = false;

            if (fileSize < 0)
            {
                negative = true;
                fileSize *= -1;
            }

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

            double friendlyFileSize = ((double)fileSize) / ((double)divideBy);

            if (negative)
            {
                friendlyFileSize *= -1;
            }

            return friendlyFileSize.ToString("N" + decimalPlaces.ToString()) + " " + suffix;
        }

        public static string FormatFileSize(double? fileSize)
        {
            return FormatFileSize(fileSize, 2);
        }

        public static string FormatFileSize(double? fileSize, int decimalPlaces)
        {
            return FormatFileSize(fileSize, decimalPlaces, false);
        }

        public static string FormatFileSize(double? fileSize, int decimalPlaces, bool singleCharacterSuffix)
        {
            if (fileSize == null)
            {
                return string.Empty;
            }
            double divideBy = 1;
            string suffix = "";
            bool negative = false;

            if (fileSize < 0)
            {
                negative = true;
                fileSize *= -1;
            }

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

            double friendlyFileSize = ((double)fileSize) / ((double)divideBy);

            if (negative)
            {
                friendlyFileSize *= -1;
            }

            return friendlyFileSize.ToString("N" + decimalPlaces.ToString()) + " " + suffix;
        }

        public static string FormatBandwidth(long? speed)
        {
            return FormatBandwidth(speed, 2);
        }

        public static string FormatBandwidth(long? speed, int decimalPlaces)
        {
            if (speed == null)
            {
                return string.Empty;
            }

            long divideBy = 1;
            string suffix = "";
            bool negative = false;

            if (speed < 0)
            {
                negative = true;
                speed *= -1;
            }

            if (speed >= Terabit)
            {
                divideBy = Terabit;
                suffix = "T";
            }
            else if (speed >= Gigibit)
            {
                divideBy = Gigibit;
                suffix = "G";
            }
            else if (speed >= Megabit)
            {
                divideBy = Megabit;
                suffix = "M";
            }
            else if (speed >= Kilobit)
            {
                divideBy = Kilobit;
                suffix = "K";
            }
            else
            {
                divideBy = 1;
            }

            suffix += "bps";

            double friendlySpeed = ((double)speed) / ((double)divideBy);

            if (negative)
            {
                friendlySpeed *= -1;
            }

            return friendlySpeed.ToString("N" + decimalPlaces.ToString()) + " " + suffix;
        }

        public static string FormatClockSpeed(long? clockSpeed)
        {
            return FormatClockSpeed(clockSpeed, 2);
        }

        public static string FormatClockSpeed(long? clockSpeed, int decimalPlaces)
        {
            if (clockSpeed == null)
            {
                return string.Empty;
            }

            long divideBy = 1;
            string suffix = "";
            bool negative = false;

            if (clockSpeed < 0)
            {
                negative = true;
                clockSpeed *= -1;
            }

            if (clockSpeed >= Terahertz)
            {
                divideBy = Terahertz;
                suffix = "THz";
            }
            else if (clockSpeed >= Gigahertz)
            {
                divideBy = Gigahertz;
                suffix = "GHz";
            }
            else if (clockSpeed >= Megahertz)
            {
                divideBy = Megahertz;
                suffix = "MHz";
            }
            else
            {
                divideBy = 1;
                suffix = "Hz";
            }

            double friendlyClockSpeed = ((double)clockSpeed) / ((double)divideBy);

            if (negative)
            {
                friendlyClockSpeed *= -1;
            }

            return friendlyClockSpeed.ToString("N" + decimalPlaces.ToString()) + " " + suffix;
        }

        public static string FormatAdUserName(string adUsername)
        {
            //http://msdn.microsoft.com/en-us/library/windows/desktop/aa380525(v=vs.85).aspx

            string[] stringArray = adUsername.Split('\\');
            if (stringArray.Count() > 1)
            {
                return stringArray[0].ToUpper() + "\\"
                    + String.Join("\\", stringArray, 1, stringArray.Count() - 1).ToLower();
            }
            return adUsername;
        }
    }
}
