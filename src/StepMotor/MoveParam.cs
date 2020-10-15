#nullable enable

namespace StepMotor
{
    public sealed class MoveParam : CommandParam
    {
        public MoveType Value { get; }

        public MoveParam(MoveType type) => Value = type;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator MoveParam(MoveType type) => new MoveParam(type);

    }
}