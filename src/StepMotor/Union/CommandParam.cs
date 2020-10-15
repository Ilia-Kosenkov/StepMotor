#nullable enable

namespace StepMotor.Union
{
    public abstract class CommandParam
    {
        public static DefaultParam Default { get; } = new DefaultParam();

        internal CommandParam() {}

        protected abstract byte AsByte();

        public static explicit operator byte(CommandParam param) => param.AsByte();

        public static implicit operator CommandParam(global::StepMotor.CommandParam.RefSearchType type) => new RefSearchType(type);
        public static implicit operator CommandParam(global::StepMotor.CommandParam.MoveType type) => new MoveType(type);
        public static implicit operator CommandParam(global::StepMotor.CommandParam.CalcType type) => new CalcType(type);
        public static implicit operator CommandParam(global::StepMotor.CommandParam.AxisParameter @param) => new AxisParameter(@param);


    }

    public sealed class DefaultParam : CommandParam
    {
        protected override byte AsByte() => 0;
    }

    public sealed class RefSearchType : CommandParam
    {
        public global::StepMotor.CommandParam.RefSearchType Value { get; }

        public RefSearchType(global::StepMotor.CommandParam.RefSearchType type) => Value = type;


        protected override byte AsByte() => (byte) Value;

        public static implicit operator RefSearchType(global::StepMotor.CommandParam.RefSearchType type) => new RefSearchType(type);

    }

    public sealed class MoveType : CommandParam
    {
        public global::StepMotor.CommandParam.MoveType Value { get; }

        public MoveType(global::StepMotor.CommandParam.MoveType type) => Value = type;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator MoveType(global::StepMotor.CommandParam.MoveType type) => new MoveType(type);

    }

    public sealed class CalcType : CommandParam
    {
        public global::StepMotor.CommandParam.CalcType Value { get; }

        public CalcType(global::StepMotor.CommandParam.CalcType type) => Value = type;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator CalcType(global::StepMotor.CommandParam.CalcType type) => new CalcType(type);

    }

    public sealed class AxisParameter : CommandParam
    {
        public global::StepMotor.CommandParam.AxisParameter Value { get; }

        public AxisParameter(global::StepMotor.CommandParam.AxisParameter @param) => Value = @param;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator AxisParameter(global::StepMotor.CommandParam.AxisParameter @param) => new AxisParameter(@param);

    }
}
