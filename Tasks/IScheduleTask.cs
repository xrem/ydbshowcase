using System.Threading;
using System.Threading.Tasks;

namespace Ydb.Showcase.Tasks;

public interface IScheduleTask
{
    public string TaskType { get; }
    public Task ExecuteAsync(CancellationToken cancellationToken);
}