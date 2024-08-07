﻿using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Library.Utilities
{
    public static class NpUtility
    {
        public delegate void TryAndIgnoreProc();
        public delegate T TryAndIgnoreProc<T>();

        /// <summary>
        /// We didn't need that exception! Did we?... DID WE?!
        /// </summary>
        public static void TryAndIgnore(TryAndIgnoreProc func)
        {
            try { func(); } catch { }
        }

        /// <summary>
        /// We didn't need that exception! Did we?... DID WE?!
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
