using System;
using System.IO;
using System.Text;

namespace AutoEDM.Diagnostics
{
    /// <summary>
    /// Grava tudo que passa pelo <see cref="Log"/> em um arquivo .log, para
    /// análise posterior de erros e inconsistências. Thread-safe (o processo roda
    /// numa thread STA separada da UI).
    /// </summary>
    public sealed class FileLogSink : IDisposable
    {
        private readonly object _gate = new object();
        private StreamWriter _writer;

        public string FilePath { get; }

        public FileLogSink(string folder = null)
        {
            folder = folder ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);
            FilePath = Path.Combine(folder, $"AutoEDM_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            _writer = new StreamWriter(FilePath, append: true, encoding: new UTF8Encoding(false))
            {
                AutoFlush = true
            };

            Log.OnMessage += Write;
        }

        private void Write(LogLevel level, string message)
        {
            lock (_gate)
            {
                _writer?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}");
            }
        }

        public void Dispose()
        {
            Log.OnMessage -= Write;
            lock (_gate)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}
