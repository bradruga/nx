namespace Nx.Components;

/// <summary>What an <see cref="NxProgressBar"/>'s <c>LabelTemplate</c> gets as <c>context</c>.</summary>
/// <param name="Value">The value, clamped to 0 … Max.</param>
/// <param name="Max">The value of a full bar.</param>
/// <param name="Percent">Value as a percentage of Max, 0 … 100.</param>
public sealed record NxProgressBarContext(double Value, double Max, double Percent)
{
    /// <summary><c>true</c> once Value reaches Max.</summary>
    public bool IsComplete => Value >= Max;
}
