namespace Sway.Widgets;

/// <summary>Slides from a start position with a velocity that decays by <c>drag</c> each second (0 &lt; drag &lt; 1).</summary>
public sealed class FrictionSimulation : Simulation
{
    readonly float _drag, _logDrag, _position, _velocity;

    public FrictionSimulation(float drag, float position, float velocity)
    {
        if (drag is <= 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(drag), "Drag must be between 0 and 1 exclusive.");
        _drag = drag;
        _logDrag = MathF.Log(drag);
        _position = position;
        _velocity = velocity;
    }

    /// <summary>Where the slide comes to rest.</summary>
    public float FinalX => _position - _velocity / _logDrag;

    public override float X(float time) => _position + _velocity * (MathF.Pow(_drag, time) - 1) / _logDrag;
    public override float Dx(float time) => _velocity * MathF.Pow(_drag, time);
    public override bool IsDone(float time) => MathF.Abs(Dx(time)) < Tolerance.Velocity;
}
