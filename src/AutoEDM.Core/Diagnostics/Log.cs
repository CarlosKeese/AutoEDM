using System;

namespace AutoEDM.Diagnostics
{
    /// <summary>
    /// Minimal logging shim. Kept intentionally trivial so it can be swapped for
    /// Serilog/NLog later without touching call sites. Writes to the console and
    /// (optionally) to a subscriber for a future UI log panel.
    /// </summary>
    public static class Log
    {
        /// <summary>Optional sink so a WinForms/WPF UI can mirror log output.</summary>
        public static event Action<LogLevel, string> OnMessage;

        public static void Info(string message) => Write(LogLevel.Info, message);
        public static void Warn(string message) => Write(LogLevel.Warn, message);
        public static void Error(string message) => Write(LogLevel.Error, message);

        public static void Error(string message, Exception ex) =>
            Write(LogLevel.Error, $"{message}{Environment.NewLine}{ex}");

        private static void Write(LogLevel level, string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} [{level,-5}] {message}";
            Console.WriteLine(line);
            OnMessage?.Invoke(level, message);
        }
    }

    public enum LogLevel
    {
        Info,
        Warn,
        Error
    }
}
