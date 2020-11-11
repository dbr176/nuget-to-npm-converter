using Microsoft.Extensions.Logging;
using NuGet.Common;
using System;
using System.Threading.Tasks;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace NugetToNpmConverter
{
    public sealed class NuGetLoggingAdapter : NuGet.Common.ILogger
    {
        private readonly ILogger<NuGetLoggingAdapter> _logger;

        public NuGetLoggingAdapter(ILogger<NuGetLoggingAdapter> logger)
        {
            _logger = logger;
        }

        private static LogLevel ConvertLogLevel(NuGet.Common.LogLevel level)
            => level switch
            {
                NuGet.Common.LogLevel.Debug => LogLevel.Debug,
                NuGet.Common.LogLevel.Verbose => LogLevel.Trace,
                NuGet.Common.LogLevel.Minimal => LogLevel.Information,
                NuGet.Common.LogLevel.Information => LogLevel.Information,
                NuGet.Common.LogLevel.Warning => LogLevel.Warning,
                NuGet.Common.LogLevel.Error => LogLevel.Error,
                _ => throw new NotImplementedException()
            };
        
        public void Log(NuGet.Common.LogLevel level, string data)
        {
            _logger.Log(ConvertLogLevel(level), data);
        }

        public void Log(ILogMessage message)
        {
            _logger.Log(ConvertLogLevel(message.Level), message.Message);
        }

        public Task LogAsync(NuGet.Common.LogLevel level, string data)
        {
            _logger.Log(ConvertLogLevel(level), data);
            return Task.CompletedTask;
        }

        public Task LogAsync(ILogMessage message)
        {
            _logger.Log(ConvertLogLevel(message.Level), message.Message);
            return Task.CompletedTask;
        }

        public void LogDebug(string data)
        {
            _logger.LogDebug(data);
        }

        public void LogError(string data)
        {
            _logger.LogError(data);
        }

        public void LogInformation(string data)
        {
            _logger.LogInformation(data);
        }

        public void LogInformationSummary(string data)
        {
            _logger.LogInformation(data);
        }

        public void LogMinimal(string data)
        {
            _logger.LogDebug(data);
        }

        public void LogVerbose(string data)
        {
            _logger.LogTrace(data);
        }

        public void LogWarning(string data)
        {
            _logger.LogWarning(data);
        }
    }
}
