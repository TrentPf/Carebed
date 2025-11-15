using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.Logging
{
	internal class SimpleFileLogger : ILoggingService, IDisposable
	{
		private readonly string _filePath;
		private StreamWriter? _writer;
		private readonly object _sync = new();
		private bool _started;

		public SimpleFileLogger(string filePath)
		{
			_filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
		}

		public Task StartAsync()
		{
			lock (_sync)
			{
				if (_started) return Task.CompletedTask;
				var dir = Path.GetDirectoryName(_filePath) ?? AppDomain.CurrentDomain.BaseDirectory;
				Directory.CreateDirectory(dir);
				var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
				_writer = new StreamWriter(fs) { AutoFlush = true };
				_started = true;
			}
			return Task.CompletedTask;
		}

		public void Log(LogMessage message)
		{
			if (message == null) return;
			// best-effort: if not started, try to start synchronously
			if (!_started)
			{
				try { StartAsync().GetAwaiter().GetResult(); }
				catch { /* swallow - logger is best-effort in MVP */ }
			}

			string line;
			try
			{
				var opts = new JsonSerializerOptions { WriteIndented = false };
				line = JsonSerializer.Serialize(message, opts);
			}
			catch
			{
				// fallback to a minimal line
				line = $"{{\"Timestamp\":\"{DateTimeOffset.UtcNow:O}\",\"Message\":\"{message.Message}\"}}";
			}

			lock (_sync)
			{
				try
				{
					_writer?.WriteLine(line);
				}
				catch
				{
					// swallow for MVP
				}
			}
		}

		public Task StopAsync()
		{
			lock (_sync)
			{
				if (!_started) return Task.CompletedTask;
				try
				{
					_writer?.Flush();
					_writer?.Dispose();
				}
				catch { }
				finally
				{
					_writer = null;
					_started = false;
				}
			}
			return Task.CompletedTask;
		}

		public void Dispose()
		{
			StopAsync().GetAwaiter().GetResult();
		}
	}
}