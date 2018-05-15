using System;
using System.Text;

namespace NetProxy.Library.Utility
{
    public static class Exceptions
    {
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
    }
}