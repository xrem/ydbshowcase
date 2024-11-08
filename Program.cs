using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ydb.Sdk.Ado;
using Ydb.Showcase.Tasks;

namespace Ydb.Showcase;

public static class Program
{
    public static async Task Main(string[] args)
    {
        ThreadPool.GetMaxThreads(out var workerThreads, out var ioCompletionThreads);
        ThreadPool.SetMaxThreads(Math.Max(30000, workerThreads), Math.Max(20000, ioCompletionThreads));
        ThreadPool.SetMinThreads(3000, 2000);

        try
        {
            var hostBuilder = Host.CreateDefaultBuilder(args);
            var host = hostBuilder
                .ConfigureHostConfiguration(builder =>
                {
                    builder
                        .AddEnvironmentVariables()
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddConsole(opts => opts.FormatterName = "CustomTimePrefixingFormatter");
                })
                .ConfigureServices((context, services) =>
                {
                    var ydbSection = context.Configuration.GetSection("YdbDatabase");
                    var connectionString = ydbSection.GetValue<string>("ConnectionString")
                                           ?? throw new InvalidOperationException("YdbDatabase ConnectionString is missing.");
                    var connectionStringBuilder = new YdbConnectionStringBuilder(connectionString)
                    {
                        MaxSessionPool = ydbSection.GetValue("MaxSessionPool", 1)
                    };

                    services.AddSingleton(new YdbConnectionFactory(connectionStringBuilder));
                    services.AddSingleton<SchemaInitializer>();

                    var minDelayMs = context.Configuration.GetValue("MinDelayMs", 10);
                    var maxDelayMs = context.Configuration.GetValue("MaxDelayMs", 20);
                    services.AddSingleton(new DelayProvider(minDelayMs, maxDelayMs));

                    services.AddSingleton<SimpleTask>();
                    services.AddSingleton<SometimesFaultingTask>();

                    var workersCount = context.Configuration.GetValue("WorkersCount", 10);
                    foreach (var i in Enumerable.Range(1, workersCount))
                    {
                        services.AddSingleton<IHostedService>(provider => ActivatorUtilities
                            .CreateInstance<ScheduleTaskWorkerService<SimpleTask>>(provider, i));
                        services.AddSingleton<IHostedService>(provider => ActivatorUtilities
                            .CreateInstance<ScheduleTaskWorkerService<SometimesFaultingTask>>(provider, i));
                    }
                })
                .Build();

            await host.Services.GetRequiredService<SchemaInitializer>().InitializeAsync();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync("Application start-up failed");
            await Console.Error.WriteLineAsync(ex.ToString());
        }
    }
}
