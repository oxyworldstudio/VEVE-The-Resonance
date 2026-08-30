using System.Threading;
using System.Threading.Tasks;

namespace VEVE.Agents
{
    /// <summary>
    /// Contract for all cognition providers. Implementations must never run
    /// work that blocks the Unity update thread; all calls are invoked as
    /// fire-and-forget Tasks whose results are applied on the main thread.
    /// </summary>
    public interface ICognitionService
    {
        string Name { get; }
        bool IsAvailable { get; }
        Task<BehaviorPlan> PlanAsync(AgentCognitionInput input, CancellationToken token = default);
    }
}
