using System;
using System.Diagnostics;

namespace quick_image_viewer.Common
{
    internal static class AppLog
    {
        public static void Write(string message)
        {
            Trace.WriteLine(message);
        }

        public static void Info(string component, string message)
        {
            Trace.WriteLine($"[{component}] {message}");
        }

        public static void Warn(string component, string message, Exception? ex = null, bool includeStackTrace = false)
        {
            WriteWithLevel("WARN", component, message, ex, includeStackTrace);
        }

        public static void Error(string component, string message, Exception? ex = null)
        {
            WriteWithLevel("ERROR", component, message, ex, includeStackTrace: false);
        }

        public static void Error(string component, string message, Exception? ex, bool includeStackTrace)
        {
            WriteWithLevel("ERROR", component, message, ex, includeStackTrace);
        }

        private static void WriteWithLevel(string level, string component, string message, Exception? ex, bool includeStackTrace)
        {
            string normalizedMessage = message.EndsWith(": ", StringComparison.Ordinal)
                ? message[..^2]
                : message.TrimEnd(':', ' ');

            if (ex == null)
            {
                Trace.WriteLine($"[{level}][{component}] {normalizedMessage}");
                return;
            }

            Trace.WriteLine($"[{level}][{component}] {normalizedMessage}: {ex.Message}");
            if (includeStackTrace && !string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                Trace.WriteLine(ex.StackTrace);
            }
        }
    }
}
