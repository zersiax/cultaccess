# Navigation regression tests

Run from the repository root:

```powershell
dotnet run --project tests\CultAccess.NavigationTests -c Release
```

The harness compiles the production `Autowalk` implementation and its movement input
postfixes directly. Small doubles replace Unity and game boundaries so complete command
and frame sequences can run without installing or redistributing the game.

Cases cover the disconnected exit reported during the opening scene, the prisoner
controller, announcement order, manual takeover and release, scripted movement, pause,
combat states, asynchronous routes, route loss and recovery, missing input, co-op input
isolation, movement inversion, stale frames, and lack of walking progress.

These tests verify control flow and input output. They do not simulate the game's physics,
pathfinding graph, or a screen reader's audio. Changes to movement also need an in-game
check that the real player reaches the selected interaction. `scripts\build.ps1` runs this
harness alongside the existing pure tests before compiling the plugin.
