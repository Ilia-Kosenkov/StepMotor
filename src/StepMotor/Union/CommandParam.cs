using System;
using System.Collections.Immutable;

#nullable enable

namespace StepMotor.Union
{
    public abstract class CommandParam
    {
        public static ImmutableArray<AxisParameter> GeneralAxisParams { get; }
            = new AxisParameter[]
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

        public static ImmutableArray<AxisParameter> RotationAxisParams { get; }
            = new AxisParameter[]
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
        public static implicit operator CommandParam(global::StepMotor.CommandParam.MoveType type) => new MoveType(type);
        public static implicit operator CommandParam(global::StepMotor.CommandParam.CalcType type) => new CalcType(type);
        public static implicit operator CommandParam(AxisParameterType @param) => new AxisParameter(@param);


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
        public AxisParameterType Value { get; }

        public AxisParameter(AxisParameterType @param) => Value = @param;


        protected override byte AsByte() => (byte)Value;

        public static implicit operator AxisParameter(AxisParameterType @param) => new AxisParameter(@param);

    }
}
