using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ydb.Sdk.Ado;
using Ydb.Showcase.Entities;
using Ydb.Showcase.Tasks;

namespace Ydb.Showcase;

public class ScheduleTaskWorkerService<T> : BackgroundService where T : IScheduleTask
{
    private readonly T _scheduleTask;
    private readonly ILogger<ScheduleTaskWorkerService<T>> _logger;
    private readonly ScheduleTaskEntity _entityRecord;
    private readonly YdbConnectionFactory _connectionFactory;
    private readonly DelayProvider _delayProvider;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ScheduleTaskWorkerService(
        int workerNumber,
        T scheduleTask,
        ILogger<ScheduleTaskWorkerService<T>> logger,
        YdbConnectionFactory connectionFactory,
        DelayProvider delayProvider)
    {
        _scheduleTask = scheduleTask;
        _logger = logger;
        _connectionFactory = connectionFactory;
        _delayProvider = delayProvider;

        _entityRecord = new ScheduleTaskEntity
        {
            Id = Guid.NewGuid(),
            Type = $"{scheduleTask.TaskType}{workerNumber}"
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await ProvisionTaskInDatabase();

        while (stoppingToken.IsCancellationRequested is false)
        {
            _entityRecord.LastStartUtc = DateTime.UtcNow;
            _entityRecord.Error = string.Empty;

            await UpdateTaskLastStartInDatabase();
            await ProcessTask(stoppingToken);
            await UpdateTaskExecutionResultInDatabase();

            await Task.Delay(_delayProvider.GetDelay(), stoppingToken);
        }
    }

    private async Task ProvisionTaskInDatabase()
    {
        _logger.LogInformation("Inserting '{taskName}' task with Id '{id}'", _entityRecord.Type, _entityRecord.Id);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        var ydbCommand = connection.CreateCommand();
        ydbCommand.Transaction = await connection.BeginTransactionAsync();
        ydbCommand.CommandText = $"""
                                  INSERT INTO ScheduleTask
                                  ({nameof(_entityRecord.Id)}, {nameof(_entityRecord.Type)}, {nameof(_entityRecord.Error)})
                                  VALUES
                                  (@Id, @Type, "")
                                  """;
        ydbCommand.Parameters.Add(new YdbParameter("@Id", _entityRecord.Id.ToString("D")));
        ydbCommand.Parameters.Add(new YdbParameter("@Type", _entityRecord.Type));
        await ydbCommand.ExecuteNonQueryAsync();
        await ydbCommand.Transaction.CommitAsync();

        _logger.LogInformation("Inserted '{taskName}' task with Id '{id}'", _entityRecord.Type, _entityRecord.Id);
    }

    private async Task UpdateTaskExecutionResultInDatabase()
    {
        _logger.LogInformation("Updating '{taskName}' task execution result", _entityRecord.Type);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        var ydbCommand = connection.CreateCommand();
        ydbCommand.Transaction = await connection.BeginTransactionAsync();
        ydbCommand.CommandText = $"""
                                  UPDATE ScheduleTask
                                  SET
                                      {nameof(_entityRecord.Error)} = @Error,
                                      {nameof(_entityRecord.LastSuccessUtc)} = @LastSuccessUtc,
                                      {nameof(_entityRecord.LastNonSuccessEndUtc)} = @LastNonSuccessEndUtc
                                  WHERE 
                                      {nameof(_entityRecord.Id)} = @Id;
                                  """;
        ydbCommand.Parameters.Add(new YdbParameter("@Id",                   _entityRecord.Id.ToString("D")));
        ydbCommand.Parameters.Add(new YdbParameter("@Error",                _entityRecord.Error));
        ydbCommand.Parameters.Add(new YdbParameter("@LastSuccessUtc",       DbType.DateTime2, _entityRecord.LastSuccessUtc));
        ydbCommand.Parameters.Add(new YdbParameter("@LastNonSuccessEndUtc", DbType.DateTime2, _entityRecord.LastNonSuccessEndUtc));

        try
        {
            await ydbCommand.ExecuteNonQueryAsync();
            await ydbCommand.Transaction.CommitAsync();
            _logger.LogInformation("Updated '{taskName}' task execution result", _entityRecord.Type);
        }
        catch (YdbException e)
        {
            _logger.LogError(e, "Failed to update '{taskName}' task execution result", _entityRecord.Type);
            await ydbCommand.Transaction.RollbackAsync();
        }
    }

    private async Task UpdateTaskLastStartInDatabase()
    {
        _logger.LogInformation("Updating '{taskName}' task start time", _entityRecord.Type);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        var ydbCommand = connection.CreateCommand();
        ydbCommand.Transaction = await connection.BeginTransactionAsync();
        ydbCommand.CommandText = $"""
                                  UPDATE ScheduleTask
                                  SET
                                      {nameof(_entityRecord.Error)} = @Error,
                                      {nameof(_entityRecord.LastStartUtc)} = @LastStartUtc
                                  WHERE 
                                      {nameof(_entityRecord.Id)} = @Id;
                                  """;
        ydbCommand.Parameters.Add(new YdbParameter("@Id",           _entityRecord.Id.ToString("D")));
        ydbCommand.Parameters.Add(new YdbParameter("@Error",        _entityRecord.Error));
        ydbCommand.Parameters.Add(new YdbParameter("@LastStartUtc", DbType.DateTime2, _entityRecord.LastStartUtc));

        try
        {
            await ydbCommand.ExecuteNonQueryAsync();
            await ydbCommand.Transaction.CommitAsync();
            _logger.LogInformation("Updated '{taskName}' task start time", _entityRecord.Type);
        }
        catch (YdbException e)
        {
            _logger.LogError(e, "Failed to update '{taskName}' task start time", _entityRecord.Type);
            await ydbCommand.Transaction.RollbackAsync();
        }
    }

    private async Task ProcessTask(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            await _scheduleTask.ExecuteAsync(stoppingToken);
            _entityRecord.LastSuccessUtc = DateTime.UtcNow;
            _entityRecord.LastNonSuccessEndUtc = null;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Error while processing schedule task {type}", _scheduleTask.TaskType);
            _entityRecord.LastSuccessUtc = null;
            _entityRecord.LastNonSuccessEndUtc = DateTime.UtcNow;
            _entityRecord.Error = e.ToString();
        }
    }
}