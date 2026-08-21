using System.Collections.Generic;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Surfaces the base indoctrination platform while a saved recruit is still waiting.
    ///
    /// The game stores pending recruits in <c>DataManager.Followers_Recruit</c>, but after a
    /// load it does not instantiate their <c>FollowerRecruit</c> interaction until the player
    /// comes within eight metres of <c>BiomeBaseManager.RecruitSpawnLocation</c>. That leaves
    /// no world object for an on-demand scan to name or route to at precisely the point where
    /// onboarding locks every ordinary interaction. This synthetic target exposes only the
    /// game's saved pending state and authored spawn transform; it does not spawn or recruit
    /// anything itself.
    /// </summary>
    internal static class PendingRecruitTarget
    {
        public static void Add(
            Vector3 origin,
            float radiusSquared,
            List<PointOfInterest> found)
        {
            DataManager manager;
            BiomeBaseManager biome;
            try
            {
                manager = DataManager.Instance;
                biome = BiomeBaseManager.Instance;
                if (manager == null || biome == null ||
                    PlayerFarming.Location != FollowerLocation.Base ||
                    manager.Followers_Recruit == null ||
                    manager.Followers_Recruit.Count == 0)
                    return;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not inspect pending recruits: {e.Message}");
                return;
            }

            // Once the real interaction exists it has the authoritative label and state.
            // Do not make the player cycle through a synthetic and real copy of one recruit.
            foreach (var point in found)
                if (point.Interaction is FollowerRecruit)
                    return;

            var spawn = biome.RecruitSpawnLocation == null
                ? null
                : biome.RecruitSpawnLocation.transform;
            if (spawn == null || (spawn.position - origin).sqrMagnitude > radiusSquared)
                return;

            var target = new PointOfInterest
            {
                Transform = spawn,
                Name = "Indoctrinate new follower",
                Kind = PoiKind.Interactable,
                InteractionRole = InteractionRole.Story,
                IsPendingRecruit = true,
            };
            found.Add(target);
            Diagnostics.NavigationDiagnostics.LogPendingRecruitScan(
                target, manager.Followers_Recruit.Count);
        }
    }
}
