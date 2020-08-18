#nullable enable
using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;

namespace StepMotor
{
    public static class MotorExtensions
    {
        public static Task<Reply> MoveToPosition(
            IAsyncMotor motor,
            int position,
            CommandParam.MoveType type = CommandParam.MoveType.Absolute,
            MotorBank motorOrBank = default)
            => motor?.SendCommandAsync(Command.MoveToPosition, position, type, motorOrBank)
               ?? throw new ArgumentNullException(nameof(motor));

        public static Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank = default)
            => GetAxisParametersAsync(motor, CommandParam.RotationAxisParams, motorOrBank);

        public static Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank = default)
            => GetAxisParametersAsync(motor, CommandParam.GeneralAxisParams, motorOrBank);

        public static Task<ImmutableDictionary<CommandParam.AxisParameter, int>>
            GetAxisParametersAsync(
                this IAsyncMotor motor,
                MotorBank motorOrBank,
                params CommandParam.AxisParameter[] @params)
            => GetAxisParametersAsync(motor, @params.ToImmutableArray(), motorOrBank);
        
        public static async Task<ImmutableDictionary<CommandParam.AxisParameter, int>>
            GetAxisParametersAsync(
                this IAsyncMotor motor,
                ImmutableArray<CommandParam.AxisParameter> @params,
                MotorBank motorOrBank = default)
        {
            _ = motor ?? throw new ArgumentNullException(nameof(motor));

            var builder = ImmutableDictionary.CreateBuilder<CommandParam.AxisParameter, int>();

            foreach (var param in @params)
            {
                try
                {
                    if (await motor.SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank)
                        is { IsSuccess: true, ReturnValue: var returnVal })
                        builder.Add(param, returnVal);
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            return builder.ToImmutable();
        }

    }
}
