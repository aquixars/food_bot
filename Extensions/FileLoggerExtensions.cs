using System.Text;
using fobot.Logging;

namespace fobot.Extensions;

public static class FileLoggerExtensions
{
    public static ILoggerFactory AddFile(this ILoggerFactory factory, string filesPath)
    {
        factory.AddProvider(new FileLoggerProvider(filesPath));
        return factory;
    }
}

public class FileLogger : ILogger
{
    private readonly string _filesFolder;

    private AppFileLogEnum _appFileLog;

    private static object _lock = new object();

    public FileLogger(AppFileLogEnum appFileLog)
    {
        _filesFolder = $"{GlobalVariables.LogsPath}{Path.DirectorySeparatorChar}";
        _appFileLog = appFileLog;
    }

    public FileLogger(string path, AppFileLogEnum appFileLog)
    {
        _filesFolder = path;
        _appFileLog = appFileLog;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (formatter != null)
        {
            lock (_lock)
            {
                FileStream fs;

                string timeStr, fileName;
                timeStr = DateTime.Now.ToShortDateString();
                timeStr = timeStr.Replace('/', '_');
                timeStr = timeStr.Replace('.', '_');
                fileName = $"{timeStr}_{Enum.GetName(typeof(AppFileLogEnum), _appFileLog)}.log";
                string filePath = $"{_filesFolder}{fileName}";
                FileInfo fi = new FileInfo(filePath);
                if (fi.Exists)
                {
                    fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                }
                else
                {
                    if (!Directory.Exists(filePath))
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                }
                var sw = new StreamWriter(fs, Encoding.UTF8);
                sw.Write(
                    logLevel != LogLevel.Error
                        ? $"[{DateTime.Now:HH:mm:ss.fff}]{(logLevel == LogLevel.Information ? "" : $" [{logLevel}]")} {formatter(state, exception)}{Environment.NewLine}"
                        : $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] {GetErrorLog(exception, formatter(state, exception))}{Environment.NewLine}");
                sw.Close();
                fs.Close();
            }
        }
    }

    private string GetErrorLog(Exception ex, string header)
    {
        if (ex == null) return "";
        var text = $@"**************************   ИСКЛЮЧЕНИЕ ""{header}""    **************************
    {ExceptionInfo(ex)}";
        //if (ex is DbEntityValidationException)
        //{
        //    string newStr = string.Join(Environment.NewLine, (ex as DbEntityValidationException).EntityValidationErrors.Select((error, index) =>
        //        (index + 1).ToString() + ": " + error.ValidationErrors.FirstOrDefault().ErrorMessage).ToArray());
        //    text += $"{Environment.NewLine} ----EntityValidationErrors----" + newStr;
        //}
        var innerException = ex.InnerException;
        var level = 0;
        while (innerException != null)
        {
            text += $@"{Environment.NewLine}{Environment.NewLine}#################### Внутреннее исключение (InnerException). Уровень {++level} ####
    {ExceptionInfo(innerException)}
    ############# Конец внутреннего исключения (InnerException). Уровень {level}";
            innerException = innerException.InnerException;
        }
        text += $@"{Environment.NewLine}-------------------------------------------------   КОНЕЦ ИСКЛЮЧЕНИЯ ""{header}""    --------------------{Environment.NewLine}";
        return text;
    }

    private static string ExceptionInfo(Exception ex)
    {
        return ex == null ? "" : $@"Тип: {ex.GetType().Name.Trim()}
    Сообщение: {ex.Message.Trim()}
    StackTrace: {ex.StackTrace?.Trim()}";
    }
}

public class FileLoggerProvider : ILoggerProvider
{
    private string _path;

    public FileLoggerProvider(string _filesFolder)
    {
        _path = _filesFolder;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_path, AppFileLogEnum.Common);
    }

    public ILogger CreateLogger(AppFileLogEnum appFileLog = AppFileLogEnum.Common)
    {
        return new FileLogger(_path, appFileLog);
    }

    public void Dispose()
    {
    }
}