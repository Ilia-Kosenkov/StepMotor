//     MIT License
//     
//     Copyright(c) 2018-2020 Ilia Kosenkov
//     
//     Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//     
//     The above copyright notice and this permission notice shall be included in all
//     copies or substantial portions of the Software.
//     
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//     SOFTWARE.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StepMotor
{
    public abstract class StepMotor : IAsyncMotor
    {
        protected const int ResponseSizeInBytes = 9;
        protected const int SpeedFactor = 30;

        protected readonly ILogger? Logger;
        private protected readonly MotorId Id;


        public Address Address { get; }
        protected readonly SerialPort Port;

        protected readonly TimeSpan TimeOut;

        protected internal StepMotor(SerialPort port, Address? address, ILogger? logger, TimeSpan defaultTimeOut = default)
        {
            address ??= Address.DefaultStart;

            TimeOut = defaultTimeOut == default
                ? TimeSpan.FromMilliseconds(300)
                : defaultTimeOut;

            Address = address.Value;
            Port = port;
            Logger = logger;

            // Event listeners
            Port.NewLine = "\r";

            if (!Port.IsOpen)
            {
                //Port.ReceivedBytesThreshold = ResponseSizeInBytes;
                // Creates port
                Port.BaudRate = 9600;
                Port.Parity = Parity.None;
                Port.DataBits = 8;
                Port.StopBits = StopBits.One;
                Port.Open();
            }
            Port.DiscardInBuffer();
            Port.DiscardOutBuffer();

            Port.ErrorReceived += Port_ErrorReceived;

            Id = new MotorId(Port.PortName, Address);

            Logger?.LogInformation("{StepMotor}: Created", Id);
        }

        protected virtual void Port_ErrorReceived(object sender, SerialErrorReceivedEventArgs args)
        {
            var n = Port.BytesToRead;
            if (n <= 0) return;

            var buffer = ArrayPool<byte>.Shared.Rent(n);
            try
            {
                Port.Read(buffer, 0, n);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public abstract Task<Reply> SendCommandAsync(
            Command command, int argument, byte type,
            Address address, MotorBank motorOrBank,
            TimeSpan timeOut, CancellationToken token = default);

        public virtual Task<Reply> SendCommandAsync(
            Command command, int argument,
            CommandParam param, MotorBank motorOrBank = default) =>
            SendCommandAsync(
                command,
                argument,
                param,
                Address,
                motorOrBank,
                TimeOut);

        public async Task<int> GetPositionAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0,
                CommandParam.AxisParameter.ActualPosition,
                motorOrBank);

            if (reply.IsSuccess)
            {
                Logger?.LogInformation("{StepMotor}: Position = {Position}", Id, reply.ReturnValue);
                return reply.ReturnValue;
            }

            throw LogThenFail();
        }

        public async Task<int> GetActualPositionAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0,
                CommandParam.AxisParameter.EncoderPosition,
                motorOrBank);

            if (reply.IsSuccess)
            {
                Logger?.LogInformation("{StepMotor}: Actual position = {ActualPosition}", Id, reply.ReturnValue);
                return reply.ReturnValue;
            }

            throw LogThenFail();
        }

        public Task<Reply> MoveToPosition(int position,
            CommandParam.MoveType type = CommandParam.MoveType.Absolute,
            MotorBank motorOrBank = default) =>
            SendCommandAsync(Command.MoveToPosition, position, type, motorOrBank);


        public async Task<bool> IsTargetPositionReachedAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0,  (byte) CommandParam.AxisParameter.TargetPositionReached,
                Address,
                motorOrBank,
                default);
            if (reply.IsSuccess)
            {
                Logger?.LogInformation("{StepMotor}: Target position is reached: {IsReached}", Id, reply.ReturnValue == 1);
                return reply.ReturnValue == 1;
            }

            throw LogThenFail();
        }

        public async Task<bool> IsInMotionAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0,
                CommandParam.AxisParameter.ActualSpeed,
                motorOrBank);

            if (reply.IsSuccess)
            {
                Logger?.LogInformation("{StepMotor}: Step motor is in motion: {InMotion}", Id, reply.ReturnValue != 0);
                return reply.ReturnValue != 0;
            }

            throw LogThenFail();
        }


        public async Task StopAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.MotorStop,
                0,
                CommandParam.RefSearchType.Stop,
                motorOrBank);
            if (!reply.IsSuccess)
                throw LogThenFail();

            Logger?.LogWarning("{StepMotor}: Motor was forced to stop", Id);
        }


        public async Task<int> GetAxisParameterAsync(CommandParam.AxisParameter param, MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank);
            if (reply.IsSuccess)
            {
                Logger?.LogInformation("{StepMotor}: {AxisParam} = {Value}", Id, param, reply.ReturnValue);
                return reply.ReturnValue;
            }

            throw LogThenFail(
                $"{nameof(Command.GetAxisParameter)} failed to retrieve axis parameter.",
                "{StepMotor}: Failed retrieving {AxisParam}", param);
        }


        public virtual async Task WaitForPositionReachedAsync(CancellationToken token = default, TimeSpan timeOut = default, MotorBank motorOrBank = default)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var startTime = DateTime.Now;

                if (!await IsTargetPositionReachedAsync(motorOrBank))
                {
                    token.ThrowIfCancellationRequested();
                    var status = await GetRotationStatusAsync(motorOrBank);
                    var delayMs = Math.Max(
                        250 * Math.Abs(status[CommandParam.AxisParameter.TargetPosition] -
                                       status[CommandParam.AxisParameter.ActualPosition]) /
                        (SpeedFactor * status[CommandParam.AxisParameter.MaximumSpeed]),
                        500);

                    while (!await IsTargetPositionReachedAsync(motorOrBank))
                    {
                        token.ThrowIfCancellationRequested();
                        if (timeOut != default && (DateTime.Now - startTime) > timeOut)
                            throw new TimeoutException();
                        await Task.Delay(delayMs, token);
                    }
                }
            }
            catch (TimeoutException tEx)
            {
                LogCaughtException(tEx, "{StepMotor}: Step motor did not reach position in time");
                throw;
            }
            catch (OperationCanceledException opEx)
            {
                LogCaughtException(opEx, "{StepMotor}: Waiting has been cancelled by the caller");
                throw;
            }
            catch(Exception ex)
            {
                LogCaughtException(ex);
                throw;
            }
        }

        public virtual async Task WaitForPositionReachedAsync(
            IProgress<(int Current, int Target)> progressReporter,
            CancellationToken token = default,
            TimeSpan timeOut = default,
            MotorBank motorOrBank = default)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var startTime = DateTime.Now;

                var reply = await SendCommandAsync(
                    Command.GetAxisParameter, 0,
                    CommandParam.AxisParameter.TargetPosition,
                    motorOrBank);

                if (!reply.IsSuccess)
                    throw new InvalidOperationException("Filed to query target position.");

                var target = reply.ReturnValue;
                var current = await GetPositionAsync(motorOrBank);

                progressReporter?.Report((current, target));

                if (!await IsTargetPositionReachedAsync(motorOrBank))
                {
                    token.ThrowIfCancellationRequested();
                    var status = await GetRotationStatusAsync(motorOrBank);
                    var delayMs = Math.Max(
                        125 * Math.Abs(status[CommandParam.AxisParameter.TargetPosition] -
                                       status[CommandParam.AxisParameter.ActualPosition]) /
                        (SpeedFactor * status[CommandParam.AxisParameter.MaximumSpeed]),
                        250);

                    while (!await IsTargetPositionReachedAsync(motorOrBank))
                    {
                        token.ThrowIfCancellationRequested();
                        if (timeOut != default && (DateTime.Now - startTime) > timeOut)
                            throw new TimeoutException();
                        await Task.Delay(delayMs, token);
                        current = await GetPositionAsync(motorOrBank);
                        progressReporter?.Report((current, target));
                    }

                }

                current = await GetPositionAsync(motorOrBank);
                progressReporter?.Report((current, target));
            }
            catch (TimeoutException tEx)
            {
                LogCaughtException(tEx, "{StepMotor}: Step motor did not reach position in time");
                throw;
            }
            catch (OperationCanceledException opEx)
            {
                LogCaughtException(opEx, "{StepMotor}: Waiting has been cancelled by the caller");
                throw;
            }
            catch (Exception ex)
            {
                LogCaughtException(ex);
                throw;
            }
        }
        public virtual async Task ReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var reply = await SendCommandAsync(
                    Command.MoveToPosition,
                    0, (byte) CommandParam.MoveType.Absolute,
                    Address,
                    motorOrBank,
                    default, token);
                if (!reply.IsSuccess)
                    throw new InvalidOperationException("Failed to return to the origin.");

                token.ThrowIfCancellationRequested();

                await WaitForPositionReachedAsync(token);
            }
            catch (TimeoutException tEx)
            {
                LogCaughtException(tEx, "{StepMotor}: Step motor did not reach position in time");
                throw;
            }
            catch (OperationCanceledException opEx)
            {
                LogCaughtException(opEx, "{StepMotor}: Waiting has been cancelled by the caller");
                throw;
            }
            catch (Exception ex)
            {
                LogCaughtException(ex);
                throw;
            }
        }

        public virtual async Task ReferenceReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
        {
            try
            {
                var reply = await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Start,
                    motorOrBank);
                if (!reply.IsSuccess)
                    throw new InvalidOperationException("Failed to start reference search.");

                if (token.IsCancellationRequested)
                {
                    await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Stop, motorOrBank);
                    token.ThrowIfCancellationRequested();
                }

                var deltaMs = 200;

                while ((reply = await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Status,
                           motorOrBank))
                       .IsSuccess
                       && reply.ReturnValue != 0)
                {
                    if (token.IsCancellationRequested)
                    {
                        await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Stop,
                            motorOrBank);
                        token.ThrowIfCancellationRequested();
                    }

                    await Task.Delay(deltaMs, token);
                }
            }
            catch (TimeoutException tEx)
            {
                LogCaughtException(tEx, "{StepMotor}: Step motor did not reach position in time");
                throw;
            }
            catch (OperationCanceledException opEx)
            {
                LogCaughtException(opEx, "{StepMotor}: Waiting has been cancelled by the caller");
                throw;
            }
            catch (Exception ex)
            {
                LogCaughtException(ex);
                throw;
            }
        }

        public abstract Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(MotorBank motorOrBank = default);
        public abstract Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(MotorBank motorOrBank = default);

        public abstract Task<bool> TrySwitchToBinary();

        public override string ToString() => Id.ToString();

        public virtual void Dispose()
        {
            Logger?.LogInformation("{StepMotor}: Disposed", Id);
        }
        public virtual ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        } 

        protected Exception LogThenFail(
            string exMessage = "Failed to retrieve value.",
            string loggerMessage = "{StepMotor}: Communication failed",
            params object[] pars)
        {
            var ex = new InvalidOperationException(exMessage);
            Logger?.LogError(ex, loggerMessage, Id, pars);
            return ex;
        }

        protected void LogCaughtException<TEx>(
            TEx ex,
            string loggerMessage = "{StepMotor}: An exception was thrown",
            params object[] pars) where TEx : Exception 
            => Logger?.LogError(ex, loggerMessage, Id, pars);
    }
}