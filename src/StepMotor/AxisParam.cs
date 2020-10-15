#nullable enable

namespace StepMotor
{
    public sealed class AxisParam : CommandParam
    {
        public AxisParameterType Value { get; }

        public AxisParam(AxisParameterType @param) => Value = @param;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator AxisParam(AxisParameterType @param) => new AxisParam(@param);

    }
}