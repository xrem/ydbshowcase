using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ydb.Showcase.Tasks;

public class SometimesFaultingTask : IScheduleTask
{
    private readonly ILogger<SometimesFaultingTask> _logger;
    private readonly DelayProvider _delayProvider;

    // ReSharper disable once ConvertToPrimaryConstructor
    public SometimesFaultingTask(ILogger<SometimesFaultingTask> logger, DelayProvider delayProvider)
    {
        _logger = logger;
        _delayProvider = delayProvider;
    }

    public string TaskType => nameof(SometimesFaultingTask);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("{task} started", TaskType);

        await Task.Delay(_delayProvider.GetDelay(), cancellationToken);

        if (Random.Shared.NextDouble() < 0.5)
        {
            _logger.LogDebug("{task} faulted", TaskType);
            throw new Exception("Something went wrong during task execution");
        }

        _logger.LogDebug("{task} finished", TaskType);
    }
}