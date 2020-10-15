#nullable enable
using System;

namespace StepMotor
{
    public readonly struct RotationProgress : IEquatable<RotationProgress>
    {
        public int Current { get; }
        public int Target { get; }

        public RotationProgress(int current, int target) => (Current, Target) = (current, target);

        public void Deconstruct(out int current, out int target) => (current, target) = (Current, Target);
        public bool Equals(RotationProgress other) => Current == other.Current && Target == other.Target;

        public override bool Equals(object? obj) => obj is RotationProgress other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Current, Target);

        public static bool operator ==(RotationProgress left, RotationProgress right) => left.Equals(right);

        public static bool operator !=(RotationProgress left, RotationProgress right) => !left.Equals(right);

        public static explicit operator (int Current, int Target)(RotationProgress progress) => (progress.Current, progress.Target);
        public static implicit operator RotationProgress((int Current, int Target) progress) => new RotationProgress(progress.Current, progress.Target);
    }
}
