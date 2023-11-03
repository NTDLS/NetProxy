using ProtoBuf;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NetProxy.Library.Utilities
{
    public static class Utility
    {
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
