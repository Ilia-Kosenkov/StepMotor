#nullable enable

namespace StepMotor
{
    public sealed class CalcParam : CommandParam
    {
        public CalcType Value { get; }

        public CalcParam(CalcType type) => Value = type;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator CalcParam(CalcType type) => new CalcParam(type);

    }
}