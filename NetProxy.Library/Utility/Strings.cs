using System;
using System.Text.RegularExpressions;

namespace NetProxy.Library.Utility
{
    public static class Strings
    {
        static readonly Regex LineBreaksAndExtraSpacesTrimmer = new Regex(@"\s\s+", RegexOptions.Compiled);

        public static bool ValidateInt32(string value)
        {
            return ValidateInt32(value, int.MinValue, int.MaxValue);
        }

        public static bool ValidateInt32(string value, int min, int max)
        {
            value = (value ?? "").Trim();

            if (value.Length == 0)
            {
                return false;
            }

            Int32 numeric;
            if (Int32.TryParse(value, out numeric))
            {
                return (numeric >= min) && (numeric <= max);
            }

            return false;
        }

        public static int CountOccurences(string needle, string haystack)
        {
            return (haystack.Length - haystack.Replace(needle, "").Length) / needle.Length;
        }

        public static string SplitCamelCase(string camelCaseString)
        {
            return Regex.Replace(camelCaseString, @"(?<a>(?<!^)((?:[A-Z][a-z])|(?:(?<!^[A-Z]+)[A-Z0-9]+(?:(?=[A-Z][a-z])|$))|(?:[0-9]+)))", @" ${a}");
        }

        public static string FixDuplicateWhitespace(string str)
        {
            str = str.Replace("\t", " ").Trim();

            int length;
            do
            {
                length = str.Length;
                str = str.Replace("  ", " ");
            } while (str.Length != length);

            return str;
        }

        public static string RemoveLineBreaks(string str)
        {
            const string space = " ";

            if (string.IsNullOrWhiteSpace(str))
            {
                return string.Empty;
            }
            return str.Replace("\r\n", space).Replace("\r", space).Replace("\n", space).Replace(Environment.NewLine, space);
        }

        public static string RemoveLineBreaksAndExtraSpaces(string str)
        {
            return LineBreaksAndExtraSpacesTrimmer.Replace(RemoveLineBreaks(str), " ");
        }

        public static string RemoveSpecialCharacters(string str)
        {
            return Regex.Replace(str, "[^a-zA-Z0-9_.]+", string.Empty, RegexOptions.Compiled);
        }
    }
}
