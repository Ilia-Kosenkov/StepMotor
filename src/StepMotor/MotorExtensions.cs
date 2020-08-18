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
                MotorBank motorOrBank = default,
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


        public static Task<int> GetPositionAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank)
            => motor?.InvokeCommandAsync(
                   Command.GetAxisParameter,
                   0,
                   CommandParam.AxisParameter.ActualPosition,
                   motorOrBank)
               ?? throw new ArgumentNullException(nameof(motor));

        public static async Task<bool> IsTargetPositionReachedAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank = default)
            => motor switch
            {
                not null => 
                    await motor.InvokeCommandAsync(
                        Command.GetAxisParameter, 
                        0, 
                        CommandParam.AxisParameter.TargetPositionReached, 
                        motorOrBank) is 1,
                null => throw new ArgumentNullException(nameof(motor))
            };
    }
}