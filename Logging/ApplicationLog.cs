using fobot.Extensions;

namespace fobot.Logging;


public enum AppFileLogEnum
{
    Common
}

public class ApplicationLog
{
    public static ILoggerFactory LoggerFactory { get; set; }
    public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
    public static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);

    public static ILogger Common => new FileLogger(AppFileLogEnum.Common);
}