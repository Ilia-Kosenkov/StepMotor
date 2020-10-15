using System;
using System.Collections.Immutable;

#nullable enable

namespace StepMotor.Union
{
    public abstract class CommandParam
    {
        public static ImmutableArray<AxisParam> GeneralAxisParams { get; }
            = new AxisParam[]
            {
                AxisParameterType.TargetPosition,
                AxisParameterType.ActualPosition,
                AxisParameterType.TargetSpeed,
                AxisParameterType.ActualSpeed,
                AxisParameterType.MaximumSpeed,
                AxisParameterType.MaximumAcceleration,
                AxisParameterType.AbsoluteMaximumCurrent,
                AxisParameterType.StandbyCurrent,
                AxisParameterType.TargetPositionReached,
                AxisParameterType.ReferenceSwitchStatus,
                AxisParameterType.RightLimitSwitchStatus,
                AxisParameterType.LeftLimitSwitchStatus,
                AxisParameterType.RightLimitSwitchDisable,
                AxisParameterType.LeftLimitSwitchDisable,
                AxisParameterType.StepRatePreScaler
            }.ToImmutableArray();

        public static ImmutableArray<AxisParam> RotationAxisParams { get; }
            = new AxisParam[]
            {
                AxisParameterType.TargetPosition,
                AxisParameterType.ActualPosition,
                AxisParameterType.TargetSpeed,
                AxisParameterType.ActualSpeed,
                AxisParameterType.MaximumSpeed,
                AxisParameterType.MaximumAcceleration
            }.ToImmutableArray();
        public static DefaultParam Default { get; } = new DefaultParam();

        internal CommandParam() {}

        protected abstract byte AsByte();

        public override int GetHashCode() => HashCode.Combine(GetType(), AsByte());

        public static explicit operator byte(CommandParam param) => param.AsByte();

        public static implicit operator CommandParam(RefSearchType type) => new RefSearchParam(type);
        public static implicit operator CommandParam(MoveType type) => new MoveParam(type);
        public static implicit operator CommandParam(CalcType type) => new CalcParam(type);
        public static implicit operator CommandParam(AxisParameterType @param) => new AxisParam(@param);


    }

    public sealed class DefaultParam : CommandParam
    {
        protected override byte AsByte() => 0;
    }

    public sealed class RefSearchParam : CommandParam
    {
        public RefSearchType Value { get; }

        public RefSearchParam(RefSearchType type) => Value = type;


        protected override byte AsByte() => (byte) Value;

        public static implicit operator RefSearchParam(RefSearchType type) => new RefSearchParam(type);

    }

    public sealed class MoveParam : CommandParam
    {
        public MoveType Value { get; }

        public MoveParam(MoveType type) => Value = type;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator MoveParam(MoveType type) => new MoveParam(type);

    }

    public sealed class CalcParam : CommandParam
    {
        public CalcType Value { get; }

        public CalcParam(CalcType type) => Value = type;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator CalcParam(CalcType type) => new CalcParam(type);

    }

    public sealed class AxisParam : CommandParam
    {
        public AxisParameterType Value { get; }

        public AxisParam(AxisParameterType @param) => Value = @param;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator AxisParam(AxisParameterType @param) => new AxisParam(@param);

    }
}
