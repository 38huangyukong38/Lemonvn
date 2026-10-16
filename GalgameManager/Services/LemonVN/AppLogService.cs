using LemonVN.Contracts.Services;
using Serilog;

namespace LemonVN.Services;

public sealed class AppLogService(IAppDataPathService paths) : IDisposable
{
    private ILogger? _logger;

    public void Initialize()
    {
        if (_logger is not null) return;
        Directory.CreateDirectory(paths.LogsPath);
        _logger = new LoggerConfiguration().WriteTo.File(
            Path.Combine(paths.LogsPath, "lemonvn-.log"), rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7).CreateLogger();
    }

    public void Information(string message) => _logger?.Information("{Message}", message);
    public void Error(Exception error, string message) => _logger?.Error(error, "{Message}", message);

    public void Dispose()
    {
        (_logger as IDisposable)?.Dispose();
        _logger = null;
    }
}
