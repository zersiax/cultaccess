using System;
using CultAccess.Navigation;
using CultAccess.Speech;
using UnityEngine;

internal static class Program
{
    private static int _passed, _failed;
    private static PointOfInterest _target;

    private static int Main()
    {
        Run("disconnected exit preserves refusal without enabling autowalk", () =>
        {
            Navigator.OnTrack = () =>
            {
                Navigator.TrackedTarget = _target;
                Navigator.WaitingForRoute = true;
                Speaker.Say("No walkable route to east exit from here.");
            };
            Autowalk.Toggle();
            Assert(!Autowalk.Engaged, "autowalk must remain off");
            Assert(Speaker.Messages.Count == 1 && Speaker.Messages[0].StartsWith("No walkable route"),
                "the refusal must be the only announcement");
        });
        Run("existing blocked guidance explains refusal again", () =>
        {
            Navigator.TrackedTarget = _target;
            Navigator.WaitingForRoute = true;
            Navigator.OnAnnounce = () => Speaker.Say("No current route to altar.");
            Autowalk.Toggle();
            Assert(!Autowalk.Engaged && Speaker.Messages.Count == 1,
                "repeat attempts must explain the blocked route without turning on");
        });
        Run("intro prisoner follows the northbound waypoint", () =>
        {
            Prisoner();
            Engage();
            Tick();
            Assert(Autowalk.Driving && Near(Autowalk.Horizontal, 0f) && Near(Autowalk.Vertical, 1f),
                "the intro controller must receive a northbound heading");
            Assert(Near(InputManager.Gameplay.GetVerticalAxis(), 1f), "the production input patch must supply it");
        });
        Run("target disappearing during a blocked-route retry preserves its refusal", () =>
        {
            Farming();
            Navigator.TrackedTarget = _target;
            Navigator.WaitingForRoute = true;
            Navigator.OnAnnounce = () =>
            {
                Navigator.TrackedTarget = null;
                Navigator.WaitingForRoute = false;
                Speaker.Say("Target is gone.");
            };
            Autowalk.Toggle();
            Assert(!Autowalk.Engaged && Speaker.Messages.Count == 1, "a stale target must not engage");
        });
        Run("recovered pending route can be walked after an explicit retry", () =>
        {
            Farming();
            Navigator.TrackedTarget = _target;
            Navigator.WaitingForRoute = true;
            Navigator.OnAnnounce = () => Navigator.WaitingForRoute = false;
            Engage(); Tick();
            Assert(Autowalk.Driving, "a route that has opened must be usable");
        });
        Run("successful start queues confirmation behind the target announcement", () =>
        {
            Farming(); Engage();
            Assert(Speaker.Messages[0] == "Guiding to altar." &&
                Speaker.Priorities[1] == SpeechPriority.Queued,
                "the target name must not be cut off by the autowalk confirmation");
        });
        Run("missing movement controller refuses with an explanation", () =>
        {
            Autowalk.Toggle();
            Assert(!Autowalk.Engaged && Speaker.Messages.Contains("autowalk.controller_unavailable"),
                "an unsupported controller must not silently engage");
        });
        Run("missing manual input disengages with an explanation", () =>
        {
            var farming = Farming(); Engage(); Tick();
            farming.rewiredPlayer = null;
            Tick();
            Assert(!Autowalk.Engaged && Speaker.Messages.Contains("autowalk.input_unavailable"),
                "unavailable manual input must release automation");
        });
        Run("an asynchronous route request waits without inventing a direct path", () =>
        {
            Farming();
            RouteFollower.CurrentWaypoint = null;
            RouteFollower.AwaitingPath = true;
            Engage(); Tick();
            Assert(Autowalk.Engaged && !Autowalk.Driving, "path computation is not a route refusal");
            RouteFollower.AwaitingPath = false;
            RouteFollower.CurrentWaypoint = new Vector3(0f, 10f);
            Tick();
            Assert(Autowalk.Driving, "driving should start when a real waypoint arrives");
        });
        Run("normal player still follows the route", () =>
        {
            Farming(); Engage(); Tick();
            Assert(Autowalk.Driving && Near(InputManager.Gameplay.GetVerticalAxis(), 1f), "normal walking");
        });
        Run("intro manual input takes over and release resumes", () =>
        {
            Prisoner(); Engage(); Tick();
            InputManager.Gameplay.IntroPlayer.Horizontal = -1f;
            Tick();
            Assert(!Autowalk.Driving, "manual steering must stop injection immediately");
            Assert(Near(InputManager.Gameplay.GetHorizontalAxis(), -1f), "manual axis must survive");
            InputManager.Gameplay.IntroPlayer.Horizontal = 0f;
            Tick();
            Assert(Autowalk.Driving, "autowalk must resume after the player releases the stick");
        });
        Run("normal manual input takes over immediately", () =>
        {
            var farming = Farming(); Engage(); Tick();
            farming.rewiredPlayer.Horizontal = -1f;
            Tick();
            Assert(!Autowalk.Driving, "the preceding frame must not override manual input");
        });
        Run("own injected input is never mistaken for manual steering", () =>
        {
            Farming(); Engage();
            for (var i = 0; i < 5; i++) { Tick(); Assert(Autowalk.Driving, "continuous driving"); }
        });
        Run("intro scripted movement and kneeling retain control", () =>
        {
            var prisoner = Prisoner(); Engage(); Tick();
            prisoner.GoToAndStopping = true;
            Tick(); Assert(!Autowalk.Driving, "scripted movement must win");
            prisoner.GoToAndStopping = false;
            NavigatorPlayer.Player.GetComponent<StateMachine>().CURRENT_STATE = StateMachine.State.CustomAnimation;
            Tick(); Assert(!Autowalk.Driving, "kneeling must not receive movement");
        });
        Run("paused game immediately stops injecting axes", () =>
        {
            Farming(); Engage(); Tick();
            Time.timeScale = 0f;
            Tick(); Assert(!Autowalk.Driving, "pause must clear the previous heading");
        });
        Run("normal combat state never receives walking input", () =>
        {
            var farming = Farming(); Engage(); Tick();
            farming.state.CURRENT_STATE = StateMachine.State.Attacking;
            Tick(); Assert(!Autowalk.Driving, "attacking must retain its own axes");
        });
        Run("lost route disengages without interrupting its explanation", () =>
        {
            Farming(); Engage(); Tick();
            Speaker.Messages.Clear();
            Speaker.Say("No walkable route to altar from here.");
            Navigator.WaitingForRoute = true;
            RouteFollower.CurrentWaypoint = null;
            Tick();
            Assert(!Autowalk.Engaged && !Autowalk.Driving, "route loss must turn autowalk off");
            Assert(Speaker.Messages.Count == 1, "route failure must not be interrupted");
        });
        Run("no target does not claim autowalk started", () =>
        {
            Navigator.OnTrack = () => Speaker.Say("No targets nearby.");
            Autowalk.Toggle();
            Assert(!Autowalk.Engaged && Speaker.Messages.Count == 1, "no-target refusal");
        });
        Run("second local player's input remains untouched", () =>
        {
            Farming(); Engage(); Tick();
            var other = new PlayerFarming();
            Assert(Near(RewiredGameplayInputSource.ApplyPostfix(false, other, -0.6f), -0.6f),
                "co-op input must remain the caller's own");
        });
        Run("inverted movement does not reverse the route", () =>
        {
            Prisoner(); InputManager.Gameplay.InvertMovement = true;
            Engage(); Tick();
            Assert(Near(InputManager.Gameplay.GetVerticalAxis(), 1f), "heading is in world coordinates");
        });
        Run("stale frames release injected input", () =>
        {
            Farming(); Engage(); Tick();
            Time.frameCount += 2;
            Assert(!Autowalk.Driving, "a missed update must not hold a movement key");
        });
        Run("stuck intro walking stops with an explanation", () =>
        {
            Prisoner(); Engage(); Tick();
            Time.unscaledTime += AutowalkPolicy.StuckSeconds;
            Tick();
            Assert(!Autowalk.Engaged, "a blocked prisoner must not walk indefinitely");
            Assert(Speaker.Messages.Contains("autowalk.no_progress"), "stuck reason must be spoken");
        });
        Console.WriteLine($"Navigation regression tests: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        Reset();
        try { test(); _passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { _failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    private static void Reset()
    {
        Autowalk.Disengage("test reset");
        Autowalk.Available = true;
        PlayerFarming.Instance = null;
        NavigatorPlayer.Player = new Transform();
        NavigatorPlayer.Player.Add(new StateMachine());
        InputManager.Gameplay = new RewiredGameplayInputSource();
        _target = new PointOfInterest { AimPosition = new Vector3(0f, 10f) };
        Navigator.TrackedTarget = null;
        Navigator.WaitingForRoute = false;
        Navigator.OnTrack = () => { Navigator.TrackedTarget = _target; Speaker.Say("Guiding to altar."); };
        Navigator.OnAnnounce = null;
        RouteFollower.CurrentWaypoint = new Vector3(0f, 10f);
        RouteFollower.AwaitingPath = false;
        Time.frameCount = 10; Time.unscaledTime = 0f; Time.timeScale = 1f;
        Speaker.Messages.Clear();
        Speaker.Priorities.Clear();
        CultAccess.Plugin.Log.Lines.Clear();
    }
    private static PlayerFarming Farming()
    {
        var farming = NavigatorPlayer.Player.Add(new PlayerFarming());
        farming.state = NavigatorPlayer.Player.GetComponent<StateMachine>();
        PlayerFarming.Instance = farming;
        return farming;
    }
    private static PlayerPrisonerController Prisoner() =>
        NavigatorPlayer.Player.Add(new PlayerPrisonerController());
    private static void Engage() { Autowalk.Toggle(); Assert(Autowalk.Engaged, "autowalk should engage"); }
    private static void Tick() { Time.frameCount++; Autowalk.Tick(); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}
