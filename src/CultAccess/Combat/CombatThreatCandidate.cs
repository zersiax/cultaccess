using CultAccess.Audio;
using UnityEngine;

namespace CultAccess.Combat
{
    internal struct CombatThreatCandidate
    {
        internal bool Valid;
        internal GameObject Source;
        internal Vector3 CuePosition;
        internal CueId CueKind;
        internal string Kind;
        internal float TimeToImpact;
        internal float ClosestDistance;
        internal float CombinedRadius;
        internal float CurrentDistance;

        internal static void ChooseSoonest(
            CombatThreatCandidate candidate,
            ref CombatThreatCandidate best)
        {
            if (!best.Valid || candidate.TimeToImpact < best.TimeToImpact)
                best = candidate;
        }
    }
}
