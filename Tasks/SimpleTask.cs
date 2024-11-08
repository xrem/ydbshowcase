using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ydb.Showcase.Tasks;

public class SimpleTask : IScheduleTask
{
    private readonly ILogger<SimpleTask> _logger;
    private readonly DelayProvider _delayProvider;

    // ReSharper disable once ConvertToPrimaryConstructor
    public SimpleTask(ILogger<SimpleTask> logger, DelayProvider delayProvider)
    {
        _logger = logger;
        _delayProvider = delayProvider;
    }

    public string TaskType => nameof(SimpleTask);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("{task} started", TaskType);

        await Task.Delay(_delayProvider.GetDelay(), cancellationToken);

        _logger.LogDebug("{task} finished", TaskType);
    }
}