#nullable enable
using Godot;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

/// <summary>
/// Advances simulation ticks at the correct speed.
/// Speed only changes ticks-per-real-second. Never skips simulation steps.
/// </summary>
public partial class SimulationClockNode : Node
{
    // 1 in-game hour = 6 real seconds at Speed 1 → 1 tick per 6 seconds at 1x
    // TODO: tune in playtesting — expose as config
    [Export] public float SecondsPerTickAtSpeed1 { get; set; } = 6.0f;

    private static readonly float[] SpeedMultipliers = { 0f, 1f, 3f, 6f, 12f, 24f };

    public int SpeedLevel { get; private set; } = 1;
    public bool IsPaused => SpeedLevel == 0;

    public WorldState? State { get; set; }
    public SimulationRunner? Runner { get; set; }

    private float _accumulator;

    [Signal]
    public delegate void TickCompletedEventHandler(int currentTick);

    [Signal]
    public delegate void SpeedChangedEventHandler(int newSpeed);

    public override void _Process(double delta)
    {
        if (IsPaused || Runner == null || State == null) return;

        float multiplier = SpeedMultipliers[SpeedLevel];
        float secondsPerTick = SecondsPerTickAtSpeed1 / multiplier;

        _accumulator += (float)delta;

        while (_accumulator >= secondsPerTick)
        {
            _accumulator -= secondsPerTick;
            Runner.Tick();
            EmitSignal(SignalName.TickCompleted, State.CurrentTick);
        }
    }

    public void SetSpeed(int level)
    {
        SpeedLevel = Mathf.Clamp(level, 0, SpeedMultipliers.Length - 1);
        _accumulator = 0f;
        EmitSignal(SignalName.SpeedChanged, SpeedLevel);
    }

    public void TogglePause()
    {
        SetSpeed(IsPaused ? 1 : 0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            switch (key.Keycode)
            {
                case Key.Space:
                    TogglePause();
                    break;
                case Key.Key1:
                    SetSpeed(1);
                    break;
                case Key.Key2:
                    SetSpeed(2);
                    break;
                case Key.Key3:
                    SetSpeed(3);
                    break;
                case Key.Key4:
                    SetSpeed(4);
                    break;
                case Key.Key5:
                    SetSpeed(5);
                    break;
            }
        }
    }
}
