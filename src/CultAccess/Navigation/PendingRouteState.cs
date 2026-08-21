namespace CultAccess.Navigation
{
    /// <summary>Evidence and retry timing retained while a selected route is temporary.</summary>
    internal sealed class PendingRouteState
    {
        public ReachabilitySnapshot InitialSnapshot;
        public ReachabilitySnapshot LastSnapshot;
        public RouteBlockerInspection Blockers;
        public float NextRetryAt;
        public bool RetryRequested;
        public string RetryTrigger;
    }
}
