﻿#nullable enable
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using StepMotor.Union;

namespace StepMotor
{
    public static class MotorExtensions
    {

        public static Task<Reply> 
            MoveToPosition(
            IAsyncMotor motor,
            int position,
            CommandParam.MoveType type = CommandParam.MoveType.Absolute,
            MotorBank motorOrBank = default)
            => motor?.SendCommandAsync(Command.MoveToPosition, position, type, motorOrBank)
               ?? throw new ArgumentNullException(nameof(motor));

        public static Task<ImmutableDictionary<AxisParameter, int>> 
            GetStatusAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank = default)
            => GetAxisParametersAsync(motor, Union.CommandParam.RotationAxisParams, motorOrBank);

        public static Task<ImmutableDictionary<AxisParameter, int>> 
            GetRotationStatusAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank = default)
            => GetAxisParametersAsync(motor, Union.CommandParam.GeneralAxisParams, motorOrBank);

        public static Task<int>
            GetAxisParameterAsync(
                this IAsyncMotor motor,
                AxisParameter param,
                MotorBank motorOrBank)
            => motor?.InvokeCommandAsync(
                   Command.GetAxisParameter,
                   0,
                   AxisParameterType.ActualPosition,
                   motorOrBank)
               ?? throw new ArgumentNullException(nameof(motor));

        public static Task<ImmutableDictionary<AxisParameter, int>>
            GetAxisParametersAsync(
                this IAsyncMotor motor,
                MotorBank motorOrBank = default,
                params AxisParameter[] @params)
            => GetAxisParametersAsync(motor, @params.ToImmutableArray(), motorOrBank);

        public static async Task<ImmutableDictionary<AxisParameter, int>>
            GetAxisParametersAsync(
                this IAsyncMotor motor,
                ImmutableArray<AxisParameter> @params,
                MotorBank motorOrBank = default)
        {
            _ = motor ?? throw new ArgumentNullException(nameof(motor));

            var builder = ImmutableDictionary.CreateBuilder<AxisParameter, int>();

            foreach (var param in @params)
            {
                try
                {
                    if (await motor.SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank)
                        is { IsSuccess: true, ReturnValue: var returnVal })
                        builder.Add(param, returnVal);
                }
                catch
                {
                    // ignore
                }
            }

            return builder.ToImmutable();
        }


        public static Task<int> 
            GetPositionAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank)
            => motor?.InvokeCommandAsync(
                   Command.GetAxisParameter,
                   0,
                   AxisParameterType.ActualPosition,
                   motorOrBank)
               ?? throw new ArgumentNullException(nameof(motor));

        public static Task<int> 
            GetActualPositionAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank)
            => motor?.InvokeCommandAsync(
                   Command.GetAxisParameter,
                   0,
                   AxisParameterType.EncoderPosition,
                   motorOrBank)
               ?? throw new ArgumentNullException(nameof(motor));

        public static async Task<bool> 
            IsTargetPositionReachedAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank = default)
            => motor switch
            {
                not null => 
                    await motor.InvokeCommandAsync(
                        Command.GetAxisParameter, 
                        0, 
                        AxisParameterType.TargetPositionReached, 
                        motorOrBank) is 1,
                null => throw new ArgumentNullException(nameof(motor))
            };

        public static async Task<bool> 
            IsInMotionAsync(
            this IAsyncMotor motor,
            MotorBank motorOrBank = default)
            => motor switch
            {
                not null =>
                    await motor.InvokeCommandAsync(
                        Command.GetAxisParameter,
                        0,
                        AxisParameterType.ActualSpeed,
                        motorOrBank) is not 0,
                null => throw new ArgumentNullException(nameof(motor))
            };
    }
}