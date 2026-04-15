using AiMultiAgent.Stats.Service.Data;

namespace AiMultiAgent.Stats.Service.Models;

public sealed class RunProjection
{
    public StatsRunEntity Run { get; init; } = new();
    public List<StatsFileEntity> Files { get; init; } = [];
    public List<StatsToolExecutionEntity> Tools { get; init; } = [];
}
