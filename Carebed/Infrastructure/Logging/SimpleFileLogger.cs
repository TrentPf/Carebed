using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.MessageEnvelope;
using System.Collections.Concurrent;
using System.Text.Json;
using System.IO;

namespace Carebed.Infrastructure.Logging
{
    /// <summary>
    /// Provides a simple file-based logging service that writes log messages to a specified file.
    /// </summary>
    /// <remarks>This logger supports asynchronous logging by enqueuing messages and processing them in the
    /// background. It ensures thread safety and optimizes I/O performance by batching writes. The logger must be
    /// started using <see cref="Start"/> before logging messages and should be stopped using <see
    /// cref="Stop"/> or disposed via <see cref="Dispose"/> to release resources properly.</remarks>
    public class SimpleFileLogger : IFileLoggingService, IDisposable
    {
        #region Fields and Properties

        private string _filePath;
        private StreamWriter? _writer;
        private readonly object _sync = new();
        private bool _started;

        // Queue for log messages
        private readonly ConcurrentQueue<IMessageEnvelope> _queue = new();

        // Cancellation token for the background worker - used to stop processing
        private CancellationTokenSource? _cts;

        // Background worker task - enables asynchronous log processing
        private Task? _worker;

        // Maximum allowed file size in bytes before trimming occurs (default 10 MB)
        private long _maxFileSizeBytes = 10 * 1024 * 1024;

        // After trim, keep this many bytes from the end of the log (default 50% of max)
        private long _trimToBytes => Math.Max(1024, _maxFileSizeBytes / 2);

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for SimpleFileLogger.
        /// </summary>
        /// <param name="filePath"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SimpleFileLogger(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Configure maximum log file size in bytes. Must be called before Start to avoid races.
        /// </summary>
        public void SetMaxFileSizeBytes(long bytes)
        {
            if (bytes < 1024) throw new ArgumentOutOfRangeException(nameof(bytes));
            lock (_sync)
            {
                if (_started) throw new InvalidOperationException("Cannot change max file size while started.");
                _maxFileSizeBytes = bytes;
            }
        }

        /// <summary>
        /// Changes the file path used by the logger.
        /// </summary>
        /// <param name="newFilePath">The new file path to be used. Cannot be <see langword="null"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown if the logger has already been started.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="newFilePath"/> is <see langword="null"/>.</exception>
        public bool ChangeFilePath(string newFilePath)
        {
            bool result = false;
            lock (_sync)
            {
                if (_started)
                {
                    throw new InvalidOperationException("Cannot change file path while logger is started.");
                }
                _filePath = newFilePath ?? throw new ArgumentNullException(nameof(newFilePath));
                result = true;
            }
            return result;
        }

        /// <summary>
        /// Starts the logger by initializing the file stream and background worker.
        /// </summary>
        /// <returns></returns>
        public Task Start()
        {
            // Synchron - ensure only one start operation at a time
            lock (_sync)
            {
                // Check if already started - if so, do nothing
                if (_started) return Task.CompletedTask;

                // Ensure directory exists
                var dir = Path.GetDirectoryName(_filePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(dir);

                // Open file stream for appending log messages
                var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs) { AutoFlush = false };

                // Trim file if it's already too large
                EnsureFileSizeLimitInternalLocked();

                // Mark as started
                _started = true;

                // Set up cancellation token 
                _cts = new CancellationTokenSource();

                // Start background worker to process log queue
                _worker = Task.Run(() => ProcessQueue(_cts.Token));
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the logger by cancelling the background worker and flushing remaining messages.
        /// </summary>
        /// <returns></returns>
        public Task Stop()
        {
            lock (_sync)
            {
                if (!_started) return Task.CompletedTask;
                _cts?.Cancel();
                try
                {
                    _worker?.Wait(1000);
                }
                catch { }
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
                    _cts?.Dispose();
                    _cts = null;
                    _worker = null;
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the logger by stopping it synchronously.
        /// </summary>
        public void Dispose()
        {
            Stop().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Logs an event message by enqueueing it for background processing.
        /// Places the message into a concurrent queue to be processed by the background worker.
        /// </summary>
        /// <param name="envelope"></param>
        public void Log(IMessageEnvelope envelope)
        {
            // Check for null message
            if (envelope == null) return;

            // Ensure logger is started by checking _started flag
            if (!_started)
            {
                // Attempt to start the logger synchronously
                try
                {
                    Start().GetAwaiter().GetResult();
                }
                catch
                {
                    throw new InvalidOperationException("Logger is not started and could not be started.");
                }
            }
            _queue.Enqueue(envelope);
        }

        /// <summary>
        /// Asynchronously handles a log message by enqueuing it for background processing.
        /// Should prevent bottlenecking the calling thread.
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns></returns>
        public Task HandleLogAsync(IMessageEnvelope envelope)
        {
            Log(envelope);
            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Processes the log message queue in the background. This process runs continuously until cancelled,
        /// writing log messages to the file in batches to optimize I/O performance.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task ProcessQueue(CancellationToken token)
        {
            // Flush interval - how often to flush to disk
            // This will be used to balance performance by batching writes into
            // clusters to reduce I/O overhead
            var flushInterval = TimeSpan.FromMilliseconds(200);
            var lastFlush = DateTime.UtcNow;

            // Main processing loop
            while (!token.IsCancellationRequested)
            {
                // Setup flag to track if any messages were written
                bool wrote = false;

                // Dequeue and write all available messages
                // Dequeue just means to remove from the queue
                while (_queue.TryDequeue(out var envelope))
                {
                    WriteLogLine(envelope);
                    wrote = true;
                }

                // Flush to disk if needed based on time interval
                if (wrote && (DateTime.UtcNow - lastFlush) > flushInterval)
                {
                    lock (_sync)
                    {
                        // Flush the writer to ensure all messages are written to disk
                        // This is the actual I/O operation
                        _writer?.Flush();

                        // After flushing, check file size and trim if needed.
                        EnsureFileSizeLimitInternalLocked();
                    }
                    lastFlush = DateTime.UtcNow;
                }

                // Brief delay to avoid tight loop when idle
                await Task.Delay(50, token).ConfigureAwait(false);
            }

            // Final flush on exit
            lock (_sync)
            {
                _writer?.Flush();
                EnsureFileSizeLimitInternalLocked();
            }
        }

        /// <summary>
        /// Writes a single log line to the file.
        /// </summary>
        /// <param name="message"></param>
        private void WriteLogLine(IMessageEnvelope envelope)
        {
            string line;
            try
            {
                var opts = new JsonSerializerOptions {
                    WriteIndented = false,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                line = JsonSerializer.Serialize(envelope, opts);
            }
            catch
            {
                // Fallback: log minimal info if serialization fails
                line = $"{{\"MessageId\":\"{envelope.MessageId}\",\"Timestamp\":\"{envelope.Timestamp:O}\"}}";
            }

            lock (_sync)
            {
                try
                {
                    //Console.WriteLine("Attempting to write log line: " + line);
                    _writer?.WriteLine(line);
                }
                catch
                {
                    // swallow for MVP
                }
            }
        }

        /// <summary>
        /// Trim the log file if it exceeds configured max size.
        /// Must be called with lock(_sync) held.
        /// </summary>
        private void EnsureFileSizeLimitInternalLocked()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_filePath)) return;
                // Get current length from writer's base stream if available, otherwise from file info.
                long length = -1;
                if (_writer != null && _writer.BaseStream is FileStream fsWriter)
                {
                    fsWriter.Flush();
                    length = fsWriter.Length;
                }
                else
                {
                    if (File.Exists(_filePath))
                        length = new FileInfo(_filePath).Length;
                    else
                        length = 0;
                }

                if (length <= _maxFileSizeBytes) return;

                // Close current writer so we can safely rewrite file
                try
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                }
                catch { }
                _writer = null;

                // How many bytes to keep from end
                long keep = Math.Min(_trimToBytes, length);

                // Read the tail of the file
                string tempPath = _filePath + ".tmp";
                using (var readFs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Seek to starting position
                    if (keep < readFs.Length)
                        readFs.Seek(-keep, SeekOrigin.End);
                    else
                        readFs.Seek(0, SeekOrigin.Begin);

                    // Read into buffer
                    byte[] buffer = new byte[keep];
                    int totalRead = 0;
                    while (totalRead < keep)
                    {
                        int r = readFs.Read(buffer, totalRead, (int)(keep - totalRead));
                        if (r <= 0) break;
                        totalRead += r;
                    }

                    // Optionally we can try to ensure we start at a newline boundary to keep lines intact.
                    // If the first byte isn't a newline, attempt to find the first newline in buffer and start there.
                    int startIndex = 0;
                    for (int i = 0; i < Math.Min(buffer.Length, 1024); i++)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            startIndex = i + 1;
                            break;
                        }
                    }

                    // Write tail to temp file
                    using (var tmp = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        tmp.Write(buffer, startIndex, totalRead - startIndex);
                        tmp.Flush();
                    }
                }

                // Replace original file with temp
                try
                {
                    File.Delete(_filePath);
                }
                catch { /* ignore */ }
                File.Move(tempPath, _filePath);

                // Reopen writer for append
                var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs) { AutoFlush = false };
            }
            catch
            {
                // swallow trimming errors to avoid crashing logger
            }
        }

        #endregion
    }
}