using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Logging;
using Carebed.Infrastructure.Message.LoggerMessages;
using Carebed.Infrastructure.MessageEnvelope;
using System.Threading.Tasks;

namespace Carebed.Managers
{
    public class LoggingManager : IManager, IDisposable
    {
        #region Properties and Fields

        // Log directory and file path
        private string _logDir = "";
        private string _logFileName = "";

        private bool _isSubscribed = false;

        private bool _isLoggerStarted = false;

        // Event bus for message handling
        private readonly IEventBus _eventBus;

        // Logging service for handling log messages
        private readonly IFileLoggingService _loggingService;   

        // Handler for global messages
        private readonly Action<IMessageEnvelope> _logMessageHandler;

        private readonly Action<MessageEnvelope<LoggerCommandMessage>> _logCommandHandler;

        #endregion

        #region Constructor(s)
        public LoggingManager(string logDir, string logFileName, IFileLoggingService loggingService, IEventBus eventBus)
        {
            try
            {                
                _loggingService = loggingService;
                _eventBus = eventBus;
                UpdateLogLocation(logDir, logFileName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ERROR! Encountered following exception: ", ex);
            }

            // Register the log message handler
            _logMessageHandler = HandleLogMessage;

            // Register the log command handler (wrap the async method for event bus compatibility)
            _logCommandHandler = envelope => HandleLogCommand(envelope).GetAwaiter().GetResult();
        }
        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles incoming log command messages. Processes commands such as Start, Stop, and AdjustFilePath.
        /// Is an async method to accommodate potential asynchronous operations in command handling.
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns></returns>
        private async Task HandleLogCommand(MessageEnvelope<LoggerCommandMessage> envelope)
        {
            var command = envelope.Payload.Command;
            bool successfullyExecutedCommand = false;

            switch (command)
            {
                case LoggerCommands.Start:
                    if (!_isLoggerStarted)
                    {
                        await _loggingService.Start();
                        _isLoggerStarted = true;
                        successfullyExecutedCommand = true;

                        var stopManagerResponse = new LoggerCommandAckMessage(
                        commandType: command,
                        isAcknowledged: successfullyExecutedCommand,
                        reason: null
                        );

                        var stopManagerResponseEnvelope = new MessageEnvelope<LoggerCommandAckMessage>(
                            stopManagerResponse,
                            MessageOrigins.LoggingManager,
                            MessageTypes.LoggerCommandResponse
                        );

                        await _eventBus.PublishAsync(stopManagerResponseEnvelope);
                    }
                    else
                    {
                        var startManagerResponse = new LoggerCommandAckMessage(
                        commandType: command,
                        isAcknowledged: false,
                        reason: null
                        );

                        var startManagerResponseEnvelope = new MessageEnvelope<LoggerCommandAckMessage>(
                            startManagerResponse,
                            MessageOrigins.LoggingManager,
                            MessageTypes.LoggerCommandResponse
                        );

                        await _eventBus.PublishAsync(startManagerResponseEnvelope);
                    }
                    break;
                case LoggerCommands.Stop:
                    if (_isLoggerStarted)
                    {
                        await _loggingService.Stop();
                        _isLoggerStarted = false;
                        successfullyExecutedCommand = true;

                        var stopManagerResponse = new LoggerCommandAckMessage(
                        commandType: command,
                        isAcknowledged: successfullyExecutedCommand,
                        reason: null
                        );

                        var stopManagerResponseEnvelope = new MessageEnvelope<LoggerCommandAckMessage>(
                            stopManagerResponse,
                            MessageOrigins.LoggingManager,
                            MessageTypes.LoggerCommandResponse
                        );

                        await _eventBus.PublishAsync(stopManagerResponseEnvelope);

                    }
                    else
                    {
                       var stopManagerResponse = new LoggerCommandAckMessage(
                       commandType: command,
                       isAcknowledged: false,
                       reason: null
                       );

                        var stopManagerResponseEnvelope = new MessageEnvelope<LoggerCommandAckMessage>(
                            stopManagerResponse,
                            MessageOrigins.LoggingManager,
                            MessageTypes.LoggerCommandResponse
                        );

                        await _eventBus.PublishAsync(stopManagerResponseEnvelope);
                    }

                    break;

                case LoggerCommands.AdjustLogFilePath:
                    var metadata = envelope.Payload.Metadata ?? new Dictionary<string, string>();
                    successfullyExecutedCommand = UpdateLogLocation(
                        logDir: metadata.GetValueOrDefault("LogDirectory", _logDir),
                        filePath: metadata.GetValueOrDefault("FilePath", _logFileName));

                    var adjustLogFilePathResponse = new LoggerCommandAckMessage(
                        commandType: command,
                        isAcknowledged: successfullyExecutedCommand,
                        reason: null
                    );
                    var adjustLogFilePathResponseEnvelope = new MessageEnvelope<LoggerCommandAckMessage>(
                        adjustLogFilePathResponse,
                        MessageOrigins.LoggingManager,
                        MessageTypes.LoggerCommandResponse
                    );
                    await _eventBus.PublishAsync(adjustLogFilePathResponseEnvelope);
                    break;
                case LoggerCommands.GetLogFilePath:
                    Console.WriteLine("SENDING THE ACK MESSAGE");
                    successfullyExecutedCommand = true;
                    var getPathMetadata = new Dictionary<string, string>
                    {
                        { "FilePath", Path.Combine(_logDir, _logFileName) }
                    };
                    var getPathResponse = new LoggerCommandAckMessage(
                        commandType: command,
                        isAcknowledged: true,
                        reason: null
                    )
                    {
                        Metadata = getPathMetadata
                    };
                    var getPathResponseEnvelope = new MessageEnvelope<LoggerCommandAckMessage>(
                        getPathResponse,
                        MessageOrigins.LoggingManager,
                        MessageTypes.LoggerCommandResponse
                    );
                    await _eventBus.PublishAsync(getPathResponseEnvelope);
                    return;
                default:
                    // Handle unknown or unsupported command
                    break;
            }
        }

        /// <summary>
        /// Handles incoming log messages by passing them to the logging service.
        /// </summary>
        /// <param name="envelope"></param>
        public void HandleLogMessage(IMessageEnvelope envelope)
        {
            _loggingService.Log(envelope);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Checks if the provided file path is valid.
        /// </summary>
        /// <param name="path">The file path to validate.</param>
        /// <returns>True if the file path is valid; otherwise, false.</returns>
        public static bool IsValidFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            char[] invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
            return path.IndexOfAny(invalidChars) == -1;
        }

        /// <summary>
        /// Updates the log directory and file path.
        /// </summary>
        /// <param name="logDir">The new log directory.</param>
        /// <param name="filePath">The new log file path.</param>
        public bool UpdateLogLocation(string logDir, string filePath)
        {
            bool result = false;
            // Check and create the log directory if it doesn't exist
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            // Check if the file path is valid
            if (!IsValidFilePath(filePath))
            {
                throw new ArgumentException($"Invalid file path: {filePath}", nameof(filePath));
            }

            // Check if the logger is started
            if (_isLoggerStarted)
            {
                throw new InvalidOperationException("Cannot change log location while logger is started.");
            }

            _logDir = logDir;
            _logFileName = filePath;

            result = _loggingService.ChangeFilePath(Path.Combine(_logDir, _logFileName));

            return result;
        }

        /// <summary>
        /// Disposes the logging manager and its resources.
        /// </summary>
        public void Dispose()
        {
            if(_isLoggerStarted) 
            {
                Stop();
            }

            if (_isSubscribed)
            {
                _eventBus.UnsubscribeFromGlobalMessages(_logMessageHandler);
                _eventBus.Unsubscribe<LoggerCommandMessage>(_logCommandHandler);
                _isSubscribed = false;
            }

            _isLoggerStarted = false;
            _loggingService.Dispose();            
        }

        /// <summary>
        /// Starts the logging service.
        /// </summary>
        public void Start()
        {
            if( _isLoggerStarted ) return;

            if (!_isSubscribed)
            {
                _eventBus.SubscribeToGlobalMessages(_logMessageHandler);
                _eventBus.Subscribe<LoggerCommandMessage>(_logCommandHandler);
                _isSubscribed = true;
            }

            _loggingService.Start();
            _isLoggerStarted = true;
        }

        /// <summary>
        /// Stops the logging service.
        /// </summary>
        public void Stop()
        {
            if( !_isLoggerStarted ) return;

            if (_isSubscribed)
            {
                _eventBus.UnsubscribeFromGlobalMessages(_logMessageHandler);
                _eventBus.Unsubscribe<LoggerCommandMessage>(_logCommandHandler);
                _isSubscribed = false;
            }

            _loggingService.Stop();
            _isLoggerStarted = false;
        }

        #endregion
    }
}