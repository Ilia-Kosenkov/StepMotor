using System;
using System.Collections.Immutable;

#nullable enable

namespace StepMotor
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
}
