using fobot;
using fobot.Database;
using fobot.Extensions;
using fobot.Logging;
using fobot.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<TelegramBackgroundWorker>();

builder.Services.AddDbContext<LocalDBContext>(options => options.UseSqlite(SecretsReader.ReadSection<string>("Database:ConnectionString")));
builder.Services.AddScoped<LocalDBContext>();

builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<AdminService>();

var host = builder.Build();
var loggerFactory = host.Services.GetService<ILoggerFactory>();

if (loggerFactory != null)
{
    ApplicationLog.LoggerFactory = loggerFactory;
    loggerFactory.AddFile($"{GlobalVariables.LogsPath}{Path.DirectorySeparatorChar}");
}

host.Run();