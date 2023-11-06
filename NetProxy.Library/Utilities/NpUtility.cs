using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Library.Utilities
{
    public static class NpUtility
    {
        public delegate void TryAndIgnoreProc();
        public delegate T TryAndIgnoreProc<T>();

        /// <summary>
        /// We didnt need that exception! Did we?... DID WE?!
        /// </summary>
        public static void TryAndIgnore(TryAndIgnoreProc func)
        {
            try { func(); } catch { }
        }

        /// <summary>
        /// We didnt need that exception! Did we?... DID WE?!
        /// </summary>
        public static T? TryAndIgnore<T>(TryAndIgnoreProc<T> func)
        {
            try { return func(); } catch { }
            return default;
        }

        public static string GetExceptionText(Exception ex)
        {
            StringBuilder builder = new StringBuilder();
            GetExceptionText(ex, 0, builder);
            return builder.ToString();
        }

        private static void GetExceptionText(Exception ex, int recursionLevel, StringBuilder builder)
        {
            if (recursionLevel < 10)
            {
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    builder.AppendLine(ex.Message);
                }

                if (ex.InnerException != null)
                {
                    GetExceptionText(ex.InnerException, recursionLevel + 1, builder);
                }
            }
        }

        public static string Sha256(string input)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                stringBuilder.Append(hashBytes[i].ToString("x2"));
            }

            return stringBuilder.ToString();
        }

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

            int numeric;
            if (int.TryParse(value, out numeric))
            {
                return numeric >= min && numeric <= max;
            }

            return false;
        }

        public static object IsNull(object value, object defaultValue)
        {
            if (value == null || value == DBNull.Value)
            {
                return defaultValue;
            }
            return value;
        }

        public static void EnsureNotNull<T>([NotNull] T? value, string? message = null, [CallerArgumentExpression(nameof(value))] string strName = "")
        {
            if (value == null)
            {
                if (message == null)
                {
                    throw new Exception($"Value should not be null: '{strName}'.");
                }
                else
                {
                    throw new Exception(message);
                }
            }
        }

        public static void EnsureNotNullOrEmpty([NotNull] Guid? value, [CallerArgumentExpression(nameof(value))] string strName = "")
        {
            if (value == null || value == Guid.Empty)
            {
                throw new Exception($"Value should not be null or empty: '{strName}'.");
            }
        }

        public static void EnsureNotNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string strName = "")
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new Exception($"Value should not be null or empty: '{strName}'.");
            }
        }

        public static void EnsureNotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string strName = "")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new Exception($"Value should not be null or empty: '{strName}'.");
            }
        }

        [Conditional("DEBUG")]
        public static void AssertIfDebug(bool condition, string message)
        {
            if (condition)
            {
                throw new Exception(message);
            }
        }

        public static void Assert(bool condition, string message)
        {
            if (condition)
            {
                throw new Exception(message);
            }
        }
    }
}
