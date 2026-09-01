using HarmonyLib;
using Rewired;
using UnityEngine;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Records what the curse actually aimed at, and whether the game's own auto-aim ran.
    ///
    /// `PlayerSpells.CastSpell` acquires an auto-aim target only when
    /// `InputManager.General.GetLastActiveController(playerFarming).type` is
    /// `ControllerType.Keyboard`. Read from source that means a controller player never gets
    /// the 180-degree target acquisition a keyboard player does — but source alone cannot
    /// settle it, and the obvious play test cannot either: pressing a keyboard key sets the
    /// last active controller to Keyboard, and then firing the curse *with the pad* sets it
    /// straight back before this method reads it. That test measures a race, not a feature.
    ///
    /// So it is measured instead. One line per cast, which is bounded because a cast is a
    /// keypress. The decisive fields are `lastController` and `target`: a pad cast that
    /// reports `target=none` while a keyboard cast in the same room reports a name proves the
    /// gate. `offBy` then says what it is worth — the angle between where the shot went and
    /// where the nearest hostile actually was.
    ///
    /// Log-only and never spoken, like every other probe here.
    /// </summary>
    [HarmonyPatch]
    internal static class CurseAimDiagnostics
    {
        internal static bool Enabled = true;

        private static readonly AccessTools.FieldRef<PlayerSpells, Health> AimTargetField =
            BuildAimTargetAccessor();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSpells), nameof(PlayerSpells.CastSpell))]
        private static void AfterCast(PlayerSpells __instance, bool autoAim, bool smallScale)
        {
            if (!Enabled || __instance == null) return;

            try
            {
                Report(__instance, autoAim, smallScale);
            }
            catch (System.Exception e)
            {
                // A probe must never be able to break the thing it is watching, and it must
                // never fail silently either.
                Plugin.Log.LogWarning($"[curse aim] could not report this cast: {e.Message}");
            }
        }

        private static void Report(PlayerSpells spells, bool autoAim, bool smallScale)
        {
            var player = PlayerFarming.Instance;
            var origin = spells.transform.position;

            var controller = player == null
                ? null
                : InputManager.General.GetLastActiveController(player);
            var controllerType = controller == null ? "none" : controller.type.ToString();

            var target = AimTargetField == null ? null : AimTargetField(spells);
            var targetName = target == null ? "none" : DescribeUnit(target);

            // The nearest hostile regardless of what the game chose, so a cast that acquired
            // nothing can still be scored against what it could have hit.
            var nearest = NearestHostile(origin);
            var nearestAngle = nearest == null
                ? float.NaN
                : Angle(origin, nearest.transform.position);

            var offBy = float.IsNaN(nearestAngle)
                ? float.NaN
                : Mathf.Abs(Mathf.DeltaAngle(spells.AimAngle, nearestAngle));

            var horizontal = player == null ? 0f : InputManager.Gameplay.GetHorizontalAxis(player);
            var vertical = player == null ? 0f : InputManager.Gameplay.GetVerticalAxis(player);

            Plugin.Log.LogInfo(
                $"[curse aim] lastController={controllerType} autoAim={autoAim} " +
                $"smallScale={smallScale} " +
                $"canUseKeyboard={(player == null ? "?" : player.canUseKeyboard.ToString())} " +
                $"mouseActive={InputManager.General.MouseInputActive} " +
                $"axes=({horizontal:0.00},{vertical:0.00}) " +
                $"facing={(player == null || player.state == null ? float.NaN : player.state.facingAngle):0.0} " +
                $"aimAngle={spells.AimAngle:0.0} target=\"{targetName}\" " +
                $"nearestHostile=\"{(nearest == null ? "none" : DescribeUnit(nearest))}\" " +
                $"nearestAngle={nearestAngle:0.0} offBy={offBy:0.0} " +
                $"nearestDistance={(nearest == null ? float.NaN : Vector2.Distance(origin, nearest.transform.position)):0.00}");
        }

        /// <summary>
        /// Nearest live hostile, measured the way everything else the player hears measures
        /// distance: across the ground, ignoring height.
        /// </summary>
        private static Health NearestHostile(Vector3 origin)
        {
            Health best = null;
            var bestDistance = float.MaxValue;

            var team = Health.team2;
            if (team == null) return null;

            foreach (var unit in team)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy) continue;
                if (unit.CurrentHP <= 0f) continue;

                var distance = Vector2.Distance(origin, unit.transform.position);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = unit;
            }

            return best;
        }

        private static string DescribeUnit(Health health)
        {
            var unit = health.GetComponent<UnitObject>() ?? health.GetComponentInParent<UnitObject>();
            return unit != null ? unit.GetType().Name : health.gameObject.name;
        }

        private static float Angle(Vector3 from, Vector3 to) =>
            Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        private static AccessTools.FieldRef<PlayerSpells, Health> BuildAimTargetAccessor()
        {
            try
            {
                var field = AccessTools.Field(typeof(PlayerSpells), "AimTarget");
                if (field != null)
                    return AccessTools.FieldRefAccess<PlayerSpells, Health>(field);

                Plugin.Log.LogWarning(
                    "[curse aim] PlayerSpells has no AimTarget field; casts will be reported " +
                    "without the acquired target, which is the field the question turns on.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[curse aim] could not bind AimTarget: {e.Message}");
            }

            return null;
        }
    }
}
