using System;
using System.Linq;

namespace NetProxy.Library.Utility
{
    public static class Formatters
    {
        #region Constants.

        public const long KILOBYTE = 1024;
        public const long MEGABYTE = 1048576;
        public const long GIGABYTE = 1073741824;
        public const long TERABYTE = 1099511627776L;
        public const long PETABYTE = 1125899906842624L;
        public const long EXABYTE = 1152921504606847000L;

        public const long MEGAHERTZ = 1000000L;
        public const long GIGAHERTZ = 1000000000L;
        public const long TERAHERTZ = 1000000000000L;

        public const long KILOBIT = 1000L;
        public const long MEGABIT = 1000000L;
        public const long GIGIBIT = 1000000000L;
        public const long TERABIT = 1000000000000L;

        // Used for the charting to abbreviate axis label values
        public const long THOUSAND = 1000; // Thousand
        public const long MILLION = THOUSAND * 1000; // Million
        public const long BILLION = MILLION * 1000; // Billion
        public const long TRILLION = BILLION * 1000; // Trillion

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

        public static string FormatAs1st2nd3rd(int number)
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

                if (number >= TRILLION)
                {
                    divideBy = TRILLION;
                    suffix = trillionSuffix;
                }
                else if (number >= BILLION)
                {
                    divideBy = BILLION;
                    suffix = billionSuffix;
                }
                else if (number >= MILLION)
                {
                    divideBy = MILLION;
                    suffix = millionSuffix;
                }
                else if (number >= THOUSAND)
                {
                    divideBy = THOUSAND;
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

            if (fileSize >= EXABYTE)
            {
                divideBy = EXABYTE;
                suffix = "EB";
            }
            else if (fileSize >= PETABYTE)
            {
                divideBy = PETABYTE;
                suffix = "PB";
            }
            else if (fileSize >= TERABYTE)
            {
                divideBy = TERABYTE;
                suffix = "TB";
            }
            else if (fileSize >= GIGABYTE)
            {
                divideBy = GIGABYTE;
                suffix = "GB";
            }
            else if (fileSize >= MEGABYTE)
            {
                divideBy = MEGABYTE;
                suffix = "MB";
            }
            else if (fileSize >= KILOBYTE)
            {
                divideBy = KILOBYTE;
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


            if (fileSize >= EXABYTE)
            {
                divideBy = EXABYTE;
                suffix = "EB";
            }
            else if (fileSize >= PETABYTE)
            {
                divideBy = PETABYTE;
                suffix = "PB";
            }
            else if (fileSize >= TERABYTE)
            {
                divideBy = TERABYTE;
                suffix = "TB";
            }
            else if (fileSize >= GIGABYTE)
            {
                divideBy = GIGABYTE;
                suffix = "GB";
            }
            else if (fileSize >= MEGABYTE)
            {
                divideBy = MEGABYTE;
                suffix = "MB";
            }
            else if (fileSize >= KILOBYTE)
            {
                divideBy = KILOBYTE;
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

            if (fileSize >= EXABYTE)
            {
                divideBy = EXABYTE;
                suffix = "EB";
            }
            else if (fileSize >= PETABYTE)
            {
                divideBy = PETABYTE;
                suffix = "PB";
            }
            else if (fileSize >= TERABYTE)
            {
                divideBy = TERABYTE;
                suffix = "TB";
            }
            else if (fileSize >= GIGABYTE)
            {
                divideBy = GIGABYTE;
                suffix = "GB";
            }
            else if (fileSize >= MEGABYTE)
            {
                divideBy = MEGABYTE;
                suffix = "MB";
            }
            else if (fileSize >= KILOBYTE)
            {
                divideBy = KILOBYTE;
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

            if (fileSize >= EXABYTE)
            {
                divideBy = EXABYTE;
                suffix = "EB";
            }
            else if (fileSize >= PETABYTE)
            {
                divideBy = PETABYTE;
                suffix = "PB";
            }
            else if (fileSize >= TERABYTE)
            {
                divideBy = TERABYTE;
                suffix = "TB";
            }
            else if (fileSize >= GIGABYTE)
            {
                divideBy = GIGABYTE;
                suffix = "GB";
            }
            else if (fileSize >= MEGABYTE)
            {
                divideBy = MEGABYTE;
                suffix = "MB";
            }
            else if (fileSize >= KILOBYTE)
            {
                divideBy = KILOBYTE;
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

            if (speed >= TERABIT)
            {
                divideBy = TERABIT;
                suffix = "T";
            }
            else if (speed >= GIGIBIT)
            {
                divideBy = GIGIBIT;
                suffix = "G";
            }
            else if (speed >= MEGABIT)
            {
                divideBy = MEGABIT;
                suffix = "M";
            }
            else if (speed >= KILOBIT)
            {
                divideBy = KILOBIT;
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

            if (clockSpeed >= TERAHERTZ)
            {
                divideBy = TERAHERTZ;
                suffix = "THz";
            }
            else if (clockSpeed >= GIGAHERTZ)
            {
                divideBy = GIGAHERTZ;
                suffix = "GHz";
            }
            else if (clockSpeed >= MEGAHERTZ)
            {
                divideBy = MEGAHERTZ;
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

        public static string FormatADUserName(string adUsername)
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
