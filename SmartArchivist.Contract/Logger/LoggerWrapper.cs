using Microsoft.Extensions.Logging;

namespace SmartArchivist.Contract.Logger
{
    /// <summary>
    /// Wraps Microsoft.Extensions.Logging.ILogger to provide a testable logging abstraction.
    /// </summary>
    public sealed class LoggerWrapper<TCategory> : ILoggerWrapper<TCategory>
    {
        private readonly ILogger<TCategory> _logger;
        public LoggerWrapper(ILogger<TCategory> logger) => _logger = logger;

        public void LogTrace(string messageTemplate, params object[] args)
            => _logger.LogTrace(messageTemplate, args);
        public void LogTrace(Exception exception, string messageTemplate, params object[] args)
            => _logger.LogTrace(exception, messageTemplate, args);
        public void LogDebug(string messageTemplate, params object[] args) 
            => _logger.LogDebug(messageTemplate, args);
        public void LogDebug(Exception exception, string messageTemplate, params object[] args)
            => _logger.LogDebug(exception, messageTemplate, args);
        public void LogInformation(string messageTemplate, params object[] args) 
            => _logger.LogInformation(messageTemplate, args);
        public void LogInformation(Exception exception, string messageTemplate, params object[] args)
            => _logger.LogInformation(exception, messageTemplate, args);
        public void LogWarning(string messageTemplate, params object[] args) 
            => _logger.LogWarning(messageTemplate, args);
        public void LogWarning(Exception exception, string messageTemplate, params object[] args)
            => _logger.LogWarning(exception, messageTemplate, args);
        public void LogError(string messageTemplate, params object[] args) 
            => _logger.LogError(messageTemplate, args);
        public void LogError(Exception exception, string messageTemplate, params object[] args)
            => _logger.LogError(exception, messageTemplate, args);
        public void LogCritical(string messageTemplate, params object[] args) 
            => _logger.LogCritical(messageTemplate, args);
        public void LogCritical(Exception exception, string messageTemplate, params object[] args)
            => _logger.LogCritical(exception, messageTemplate, args);
    }
}