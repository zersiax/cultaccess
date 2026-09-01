using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Holds the movement direction that walking guidance is already announcing, so the
    /// player does not have to.
    ///
    /// This is automation, and the project's default position is against it. It is here
    /// because a player asked for it, it is never on unless a key was pressed for this
    /// journey, and it can be switched off entirely in the settings menu. What kept it
    /// defensible is the shape: autowalk decides nothing. It executes the instruction
    /// guidance has already spoken, and it stops the moment guidance does. There is no
    /// second opinion about where to go, no path of its own, and nothing it can reach that
    /// pressing a direction key could not.
    ///
    /// It works by supplying the game's own movement axes rather than by moving the Lamb.
    /// Everything downstream — speed, collision, facing, the analogue speed curve, dodge
    /// direction, the game's own invert-movement setting — therefore behaves exactly as if
    /// the player were holding the stick, because as far as the game is concerned they are.
    /// <see cref="Patches.AutowalkInput"/> is the whole of the injection.
    ///
    /// The player's own input always wins. Pushing a direction takes the wheel back for as
    /// long as it is held and autowalk resumes on release; it is not an off switch, because
    /// nudging round an obstacle is the common case and having to re-engage afterwards would
    /// make the feature not worth using.
    /// </summary>
    internal static class Autowalk
    {
        /// <summary>
        /// Rewired action ids for the gameplay movement axes, as
        /// <c>RewiredGameplayInputSource.GetHorizontalAxis</c> and its vertical counterpart
        /// read them. Read raw here, before the accessibility invert those two apply, because
        /// all this needs to know is whether the player is pushing anything.
        /// </summary>
        private const int HorizontalAxisAction = 1;
        private const int VerticalAxisAction = 0;

        /// <summary>
        /// Below this the heading is noise. Standing on the waypoint produces a direction
        /// that flips every frame, which reads as a stutter rather than as arrival.
        /// </summary>
        private const float MinimumHeadingDistance = 0.05f;

        private static readonly AutowalkProgress Progress = new AutowalkProgress();

        /// <summary>
        /// The frame stamp meaning "no heading computed yet". Deliberately not
        /// <c>int.MinValue</c>: <c>Time.frameCount - int.MinValue</c> overflows to a negative
        /// number, which satisfies the staleness test and would report driving with a heading
        /// of zero — silently swallowing the player's own input instead of leaving it alone.
        /// The explicit non-negative check below is what actually rules that out; this is
        /// merely the value it checks against.
        /// </summary>
        private const int NotDriving = -1;

        private static bool _engaged;
        private static int _drivingFrame = NotDriving;
        private static float _horizontal;
        private static float _vertical;

        /// <summary>Whether the feature is offered at all. Owned by the settings menu.</summary>
        internal static bool Available = true;

        internal static bool Engaged => _engaged;

        /// <summary>
        /// Whether the input patch should substitute a heading right now.
        ///
        /// Frame-stamped rather than a plain flag, and tolerant of exactly one frame, because
        /// Unity does not promise to run our Update before the player controller's. Without
        /// the tolerance an unfavourable script order would mean autowalk never drove at all;
        /// without the stamp, an Update that stopped being called — speech switched off, the
        /// settings menu opened, an exception upstream — would leave the movement key held
        /// down forever with nothing left running to release it.
        /// </summary>
        internal static bool Driving =>
            _engaged && _drivingFrame >= 0 && Time.frameCount - _drivingFrame <= 1;

        internal static float Horizontal => _horizontal;

        internal static float Vertical => _vertical;

        /// <summary>One key to start walking and to stop again, mirroring guidance itself.</summary>
        internal static void Toggle()
        {
            if (_engaged)
            {
                Disengage("player", "Autowalk off.");
                return;
            }

            if (!Available)
            {
                Speaker.Say("Autowalk is turned off in the settings menu.");
                return;
            }

            // Starting guidance too, rather than refusing, because "select a target and walk
            // to it" is one intention and splitting it across two keys is friction with no
            // decision in the middle. TrackSelected speaks its own refusals, so a failure
            // here has already been explained and adding to it would only talk over it.
            var startedGuidance = Navigator.TrackedTarget == null;
            if (startedGuidance) Navigator.TrackSelected();
            var target = Navigator.TrackedTarget;
            if (target == null) return;

            _engaged = true;
            _drivingFrame = NotDriving;
            ResetProgress();

            Plugin.Log.LogInfo(
                $"[autowalk] engaged target=\"{target.Name}\" startedGuidance={startedGuidance}");

            // "Guiding to Shrine" has just been spoken if we started it, and naming the
            // target twice in one breath is the sort of repetition that makes a screen reader
            // tiring to listen to.
            Speaker.Say(startedGuidance ? "Autowalk on." : $"Autowalk on. Walking to {target.Name}.");
        }

        /// <summary>
        /// Stop driving. Silent by default: every caller other than the player's own key is a
        /// route ending, and guidance has already said why in its own words.
        /// </summary>
        internal static void Disengage(string reason, string announcement = null)
        {
            if (!_engaged) return;

            _engaged = false;
            _drivingFrame = NotDriving;
            _horizontal = 0f;
            _vertical = 0f;

            Plugin.Log.LogInfo($"[autowalk] disengaged reason={reason}");
            if (announcement != null) Speaker.Say(announcement);
        }

        /// <summary>
        /// Driven from the plugin's Update, alongside the navigator's own tick and under the
        /// same gate. Cheap when not engaged.
        ///
        /// Not calling this is how autowalk stops: anything that suspends the mod's update
        /// loop lets the frame stamp go stale, and <see cref="Driving"/> goes false on its own.
        /// </summary>
        internal static void Tick()
        {
            if (!_engaged) return;

            var target = Navigator.TrackedTarget;
            if (target == null)
            {
                Disengage("guidance ended");
                return;
            }

            var player = NavigatorPlayer.Resolve();
            var farming = PlayerFarming.Instance;
            if (player == null || farming == null) return;

            var position = player.position;

            // Everything from here down that stops short of driving re-anchors the progress
            // measurement, so standing still legitimately — paused, mid-animation, or because
            // the player is steering — can never be mistaken for being stuck.
            if (!CanDrive(farming))
            {
                ResetProgress(position);
                return;
            }

            if (!TryReadManualInput(farming, out var manualX, out var manualY))
            {
                ResetProgress(position);
                return;
            }

            if (AutowalkPolicy.PlayerIsSteering(manualX, manualY))
            {
                ResetProgress(position);
                return;
            }

            if (!TryHeading(target, position, out var heading))
            {
                ResetProgress(position);
                return;
            }

            _horizontal = heading.x;
            _vertical = heading.y;
            _drivingFrame = Time.frameCount;

            if (Progress.Observe(Time.unscaledTime, position.x, position.y))
                ReportNoProgress(target, position, heading);
        }

        /// <summary>
        /// Autowalk exists partly to find out where walking guidance is failing, so the one
        /// moment it demonstrably failed has to leave behind enough evidence to say why —
        /// otherwise all a session yields is "it got stuck somewhere", which is the report we
        /// already had.
        ///
        /// The decisive pair is <c>playerArea</c> against <c>targetArea</c> in the reachability
        /// line: equal areas with the Lamb going nowhere is geometry we are steering into,
        /// while different areas mean the graph believes there is no route at all and the
        /// question moves to whether the graph is stale.
        /// </summary>
        private static void ReportNoProgress(
            PointOfInterest target, Vector3 position, Vector2 heading)
        {
            var waypoint = RouteFollower.CurrentWaypoint;

            Plugin.Log.LogInfo(
                $"[autowalk] no progress target=\"{target.Name}\" kind={target.Kind} " +
                $"availability={target.Availability} " +
                $"heading=({heading.x:0.00},{heading.y:0.00}) " +
                $"position=({position.x:0.00},{position.y:0.00}) " +
                $"waypoint={(waypoint.HasValue ? $"({waypoint.Value.x:0.00},{waypoint.Value.y:0.00})" : "none")} " +
                $"finalWaypoint={RouteFollower.IsOnFinalWaypoint} " +
                $"awaitingPath={RouteFollower.AwaitingPath} " +
                $"pendingRoute={Navigator.WaitingForRoute} " +
                $"postPathApproach={target.HasPostPathApproach} " +
                $"remaining={RoutePlanarMath.Distance(position.x, position.y, target.AimPosition.x, target.AimPosition.y):0.00} " +
                $"blocked={Combat.ObstacleSonar.Blocked} " +
                $"obstacle=\"{Combat.ObstacleSonar.BlockedObstacle}\"");

            // Reuses the route gate's own probe and the existing reachability marker rather
            // than inventing a second vocabulary for the same graph facts.
            Diagnostics.NavigationDiagnostics.LogReachability(
                "autowalk-stuck",
                target,
                RouteFollower.InspectReachability(position, target.PathPosition),
                null,
                null,
                target.Availability == PoiAvailability.Locked,
                Navigator.GraphRevision);

            // Measured 2026-08-25: both no-progress events in the first session were the Lamb
            // pressed flat against a collider at distance 0.01, with the drive heading equal
            // to the obstacle direction the sonar had already reported. The route was fine and
            // the graph was fine; the straight line to the next waypoint crossed scenery. So
            // name the scenery and the direction, because "not making progress" sends the
            // player looking for a bug in the route instead of stepping around a wall.
            Disengage("no progress", StuckMessage());
        }

        /// <summary>
        /// What to say when autowalk gives up. Names the obstruction whenever the wall sonar
        /// is in contact, which is what both observed failures were.
        /// </summary>
        private static string StuckMessage()
        {
            if (!Combat.ObstacleSonar.Blocked)
                return Localization.Strings.Get("autowalk.no_progress");

            var bearing = Compass.Describe(Combat.ObstacleSonar.BlockedDirection);
            return string.IsNullOrEmpty(bearing)
                ? Localization.Strings.Get("autowalk.blocked_plain")
                : Localization.Strings.Format("autowalk.blocked", bearing);
        }

        /// <summary>
        /// The six states in which <c>PlayerController.Update</c> turns the movement axes into
        /// walking. An allowlist rather than a list of exclusions on purpose: those same two
        /// axes are also the aim direction, the building placement cursor, the map pan, the
        /// fishing reel and the dodge-roll steer, and a heading substituted into any of those
        /// would be a different feature than the one that was asked for.
        /// </summary>
        private static bool CanDrive(PlayerFarming farming)
        {
            // The game's own authority for "a script is moving the Lamb, input is not".
            if (farming.GoToAndStopping || farming.state == null) return false;
            if (Time.timeScale == 0f) return false;

            switch (farming.state.CURRENT_STATE)
            {
                case StateMachine.State.Idle:
                case StateMachine.State.Moving:
                case StateMachine.State.Idle_Winter:
                case StateMachine.State.Moving_Winter:
                case StateMachine.State.Idle_CarryingBody:
                case StateMachine.State.Moving_CarryingBody:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadManualInput(
            PlayerFarming farming, out float horizontal, out float vertical)
        {
            horizontal = 0f;
            vertical = 0f;

            var rewired = farming.rewiredPlayer;
            if (rewired == null)
            {
                // Refusing to drive is the only safe answer: without the player's own axes
                // there is no way to notice them taking over, and a movement key we cannot
                // release on demand is worse than a feature that did not start.
                Plugin.Log.LogWarning(
                    "[autowalk] no Rewired player on PlayerFarming; not driving this frame.");
                return false;
            }

            horizontal = rewired.GetAxis(HorizontalAxisAction);
            vertical = rewired.GetAxis(VerticalAxisAction);
            return true;
        }

        /// <summary>
        /// Steer at the live route waypoint, falling back to the target itself exactly where
        /// spoken guidance falls back to a direct line — so what autowalk does and what the
        /// player was told are never two different things.
        ///
        /// The waypoint is used at full precision rather than through the eight-point compass
        /// the instructions are spoken in, which is the whole reason this arrives closer to
        /// the target than following the words does.
        /// </summary>
        private static bool TryHeading(
            PointOfInterest target, Vector3 position, out Vector2 heading)
        {
            heading = Vector2.zero;

            // A pending route has no heading anyone would want followed: guidance is saying
            // "no current route, still checking", and the target may be behind a shut door.
            if (Navigator.WaitingForRoute) return false;

            var waypoint = RouteFollower.CurrentWaypoint;

            // Measured 2026-08-26: with no waypoint this fell back to the target's own
            // position and drove the straight line to it — from 34 metres out, through a
            // room wall, which is the exact thing following a route exists to prevent. The
            // fallback is only defensible for the short off-graph hop at the end of a route,
            // where guidance itself switches to a direct line and says so. Anywhere else, no
            // waypoint means no route yet, and autowalk inventing one is it deciding
            // something — which is the line principle 13 draws.
            if (!waypoint.HasValue && !target.HasPostPathApproach)
                return false;

            var goal = waypoint ?? target.AimPosition;

            var dx = goal.x - position.x;
            var dy = goal.y - position.y;
            var length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < MinimumHeadingDistance) return false;

            // Unit length deliberately. The game scales walking speed by the magnitude of the
            // pair under its default movement mode, and a shorter vector would be a slow walk
            // for no reason the player could account for.
            heading = new Vector2(dx / length, dy / length);
            return true;
        }

        private static void ResetProgress()
        {
            var player = NavigatorPlayer.Resolve();
            if (player == null) Progress.Reset(Time.unscaledTime, 0f, 0f);
            else ResetProgress(player.position);
        }

        private static void ResetProgress(Vector3 position) =>
            Progress.Reset(Time.unscaledTime, position.x, position.y);
    }
}
