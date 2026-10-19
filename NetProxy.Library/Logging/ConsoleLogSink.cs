using NetProxy.Library.Utilities;

namespace NetProxy.Library.Logging
{
    /// <summary>
    /// Writes colorized log entries to the console. Only useful when running interactively.
    /// </summary>
    public class ConsoleLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            Console.ForegroundColor = entry.Severity switch
            {
                NpLogging.Severity.Verbose => ConsoleColor.Gray,
                NpLogging.Severity.Information => ConsoleColor.White,
                NpLogging.Severity.Warning => ConsoleColor.DarkYellow,
                NpLogging.Severity.Exception => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };

            Console.WriteLine(entry.Format());
            Console.ResetColor();
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}
