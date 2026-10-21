// Boundary doubles for the game/Unity APIs. Tests execute the production autowalk and
// input postfixes, including their shared static state and actual announcement order.
using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Navigation;

namespace UnityEngine
{
    public class Component
    {
        public bool isActiveAndEnabled = true;
        public Transform transform;
        public T GetComponent<T>() where T : class => transform.GetComponent<T>();
    }
    public sealed class Transform
    {
        private readonly Dictionary<Type, object> _components = new Dictionary<Type, object>();
        public Vector3 position;
        public T GetComponent<T>() where T : class =>
            _components.TryGetValue(typeof(T), out var value) ? value as T : null;
        public T Add<T>(T value) where T : Component
        {
            value.transform = this;
            _components[typeof(T)] = value;
            return value;
        }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2();
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z = 0f) { this.x = x; this.y = y; this.z = z; }
    }
    public static class Mathf { public static float Sqrt(float value) => (float)Math.Sqrt(value); }
    public static class Time
    {
        public static int frameCount;
        public static float unscaledTime;
        public static float timeScale = 1f;
    }
}
public sealed class StateMachine : UnityEngine.Component
{
    public enum State { Idle, Moving, Idle_Winter, Moving_Winter, Idle_CarryingBody,
        Moving_CarryingBody, InActive, CustomAnimation, Attacking, Dodging }
    public State CURRENT_STATE;
}
namespace Rewired
{
    public sealed class Player
    {
        public float Horizontal, Vertical;
        public float GetAxis(int action) => action == 1 ? Horizontal : Vertical;
    }
}
public sealed class PlayerFarming : UnityEngine.Component
{
    public static PlayerFarming Instance;
    public StateMachine state;
    public bool GoToAndStopping;
    public Rewired.Player rewiredPlayer = new Rewired.Player();
}
public sealed class PlayerPrisonerController : UnityEngine.Component
{
    public bool GoToAndStopping;
}
public static class InputManager
{
    public static RewiredGameplayInputSource Gameplay = new RewiredGameplayInputSource();
}
public sealed class RewiredGameplayInputSource
{
    public Rewired.Player IntroPlayer = new Rewired.Player();
    public bool InvertMovement;
    public float GetHorizontalAxis(PlayerFarming playerFarming = null) =>
        ReadAxis(true, playerFarming);
    public float GetVerticalAxis(PlayerFarming playerFarming = null) =>
        ReadAxis(false, playerFarming);
    private float ReadAxis(bool horizontal, PlayerFarming farming)
    {
        var owner = farming ?? PlayerFarming.Instance;
        var input = owner == null ? IntroPlayer : owner.rewiredPlayer;
        var result = input == null ? 0f : input.GetAxis(horizontal ? 1 : 0);
        if (InvertMovement) result = -result;
        return ApplyPostfix(horizontal, farming, result);
    }
    public static float ApplyPostfix(bool horizontal, PlayerFarming player, float result)
    {
        var method = typeof(CultAccess.Patches.AutowalkInput).GetMethod(
            horizontal ? "SupplyHorizontalAxis" : "SupplyVerticalAxis",
            BindingFlags.NonPublic | BindingFlags.Static);
        var args = new object[] { player, result };
        method.Invoke(null, args);
        return (float)args[1];
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string method) { }
    }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix : Attribute { }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name);
    }
}
namespace CultAccess
{
    public static class Plugin { public static TestLog Log = new TestLog(); }
    public sealed class TestLog
    {
        public readonly List<string> Lines = new List<string>();
        public void LogInfo(object text) => Lines.Add(text.ToString());
        public void LogWarning(object text) => Lines.Add(text.ToString());
    }
}
namespace CultAccess.Speech
{
    public enum SpeechPriority { Now, Queued }
    public static class Speaker
    {
        public static readonly List<string> Messages = new List<string>();
        public static readonly List<SpeechPriority> Priorities = new List<SpeechPriority>();
        public static void Say(string text, SpeechPriority priority = SpeechPriority.Now)
        {
            Messages.Add(text);
            Priorities.Add(priority);
        }
    }
}
namespace CultAccess.Localization
{
    public static class Strings
    {
        public static string Get(string key) => key;
        public static string Format(string key, params object[] values) => key;
    }
}
namespace CultAccess.Combat
{
    public static class ObstacleSonar
    {
        public static bool Blocked;
        public static string BlockedObstacle = "wall";
        public static UnityEngine.Vector2 BlockedDirection;
    }
}
namespace CultAccess.Diagnostics
{
    public static class NavigationDiagnostics
    {
        public static void LogReachability(string reason, object target, object snapshot,
            object initial, object blockers, bool locked, int revision) { }
    }
}
namespace CultAccess.Navigation
{
    public enum PoiAvailability { Available, Locked }
    public sealed class PointOfInterest
    {
        public string Name = "altar", Kind = "Story";
        public PoiAvailability Availability;
        public bool HasPostPathApproach;
        public UnityEngine.Vector3 AimPosition, PathPosition;
    }
    public static class NavigatorPlayer
    {
        public static UnityEngine.Transform Player;
        public static UnityEngine.Transform Resolve() => Player;
    }
    public static class Navigator
    {
        public static PointOfInterest TrackedTarget;
        public static bool WaitingForRoute;
        public static int GraphRevision;
        public static Action OnTrack, OnAnnounce;
        public static void TrackSelected() => OnTrack?.Invoke();
        public static void AnnounceGuidance() => OnAnnounce?.Invoke();
    }
    public static class RouteFollower
    {
        public static UnityEngine.Vector3? CurrentWaypoint;
        public static bool IsOnFinalWaypoint, AwaitingPath;
        public static object InspectReachability(UnityEngine.Vector3 start, UnityEngine.Vector3 end) => null;
    }
    public static class Compass
    {
        public static string Describe(UnityEngine.Vector2 direction) => "east";
    }
}
