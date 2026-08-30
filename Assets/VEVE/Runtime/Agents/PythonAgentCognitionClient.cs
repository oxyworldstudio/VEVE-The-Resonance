using System;
using System.Threading;
using System.Threading.Tasks;

namespace VEVE.Agents
{
    /// <summary>
    /// Optional async transport stub to the Python agent sidecar (HTN deep
    /// planning, semantic memory, LLM fallback). Disabled by default and never
    /// enabled on console targets. When unavailable, AgentBridge treats every
    /// request as a graceful failure and keeps the local planner authoritative.
    /// Roadmap Phase-1: replace PlanAsync with a lock-free shared-memory ring
    /// buffer (game to sidecar) plus a gRPC unary channel (sidecar to game)
    /// generated from agents.proto, respecting the cancellation token.
    /// </summary>
    public sealed class PythonAgentCognitionClient : ICognitionService
    {
        public string Name => "python-sidecar";

        /// <summary>
        /// When false (default), the service reports unavailable and the bridge
        /// never spawns queries against it.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Health check seam: in production this is wired to the sidecar
        /// heartbeat (HealthCheck rpc) rather than a constant.
        /// </summary>
        public bool IsAvailable => Enabled;

        public Task<BehaviorPlan> PlanAsync(AgentCognitionInput input, CancellationToken token = default)
        {
            if (!IsAvailable)
                throw new InvalidOperationException("Python cognition sidecar is not enabled.");

            // TODO(roadmap Phase 1): serialize AgentCognitionInput into the shared
            // memory command ring, await PlanResult from the completion queue,
            // and map it to a BehaviorPlan without allocating on the main thread.
            throw new NotImplementedException(
                "Python sidecar transport not yet wired; the local heuristic planner is authoritative.");
        }
    }
}
