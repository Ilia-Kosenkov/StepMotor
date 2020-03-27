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
using System.Collections.Immutable;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace StepMotor
{
    public abstract class StepMotor : IAsyncMotor
    {
        protected const int ResponseSizeInBytes = 9;
        protected const int SpeedFactor = 30;

        public event StepMotorEventHandler? DataReceived;
        public event StepMotorEventHandler? ErrorReceived;

        public Address Address { get; }
        protected readonly SerialPort Port;

        protected readonly TimeSpan TimeOut;

        protected internal StepMotor(SerialPort port, Address? address, TimeSpan defaultTimeOut = default)
        {
            address ??= Address.DefaultStart;

            TimeOut = defaultTimeOut == default
                ? TimeSpan.FromMilliseconds(300)
                : defaultTimeOut;

            Address = address.Value;
            Port = port; //new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            // Event listeners
            Port.NewLine = "\r";

            if (!Port.IsOpen)
            {
                Port.ReceivedBytesThreshold = ResponseSizeInBytes;
                // Creates port
                Port.BaudRate = 9600;
                Port.Parity = Parity.None;
                Port.DataBits = 8;
                Port.StopBits = StopBits.One;
                Port.Open();
            }
            Port.DiscardInBuffer();
            Port.DiscardOutBuffer();
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

        protected virtual void OnDataReceived(StepMotorEventArgs e) 
            => DataReceived?.Invoke(this, e);

        protected virtual void OnErrorReceived(StepMotorEventArgs e) 
            => ErrorReceived?.Invoke(this, e);
        public async Task<int> GetActualPositionAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0,
                CommandParam.AxisParameter.ActualPosition,
                motorOrBank);

            if (reply.IsSuccess)
                return reply.ReturnValue;
            throw new InvalidOperationException("Failed to retrieve value.");
        }
        public Task<Reply> MoveToPosition(int position,
            CommandParam.MoveType type = CommandParam.MoveType.Absolute,
            MotorBank motorOrBank = default) =>
            SendCommandAsync(Command.MoveToPosition, position, type, motorOrBank);


        public async Task<bool> IsTargetPositionReachedAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0, CommandParam.AxisParameter.TargetPositionReached,
                motorOrBank);
            if (reply.IsSuccess)
                return reply.ReturnValue == 1;
            throw new InvalidOperationException("Failed to retrieve value.");
        }

        public async Task<bool> IsInMotionAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0,
                CommandParam.AxisParameter.ActualSpeed,
                motorOrBank);

            if (reply.IsSuccess)
                return reply.ReturnValue != 0;
            throw new InvalidOperationException("Failed to retrieve value.");
        }


        public async Task StopAsync(MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(
                Command.MotorStop,
                0,
                CommandParam.RefSearchType.Stop,
                motorOrBank);
            if (!reply.IsSuccess)
                throw new InvalidOperationException("Failed to retrieve value.");
        }


        public async Task<int> GetAxisParameterAsync(CommandParam.AxisParameter param, MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank);
            if (reply.IsSuccess)
                return reply.ReturnValue;
            throw new InvalidOperationException($"{nameof(Command.GetAxisParameter)} failed to retrieve parameter.");
        }


        public virtual async Task WaitForPositionReachedAsync(CancellationToken token = default, TimeSpan timeOut = default, MotorBank motorOrBank = default)
        {
            token.ThrowIfCancellationRequested();
            var startTime = DateTime.Now;

            if (!await IsTargetPositionReachedAsync(motorOrBank))
            {
                token.ThrowIfCancellationRequested();
                var status = await GetRotationStatusAsync(motorOrBank);
                var delayMs = Math.Max(
                    250 * Math.Abs(status[CommandParam.AxisParameter.TargetPosition] - status[CommandParam.AxisParameter.ActualPosition]) /
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
        public virtual async Task WaitForPositionReachedAsync(
            IProgress<(int Current, int Target)> progressReporter,
            CancellationToken token = default,
            TimeSpan timeOut = default,
            MotorBank motorOrBank = default)
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
            var current = await GetActualPositionAsync(motorOrBank);

            progressReporter?.Report((current, target));

            if (!await IsTargetPositionReachedAsync(motorOrBank))
            {
                token.ThrowIfCancellationRequested();
                var status = await GetRotationStatusAsync(motorOrBank);
                var delayMs = Math.Max(
                    125 * Math.Abs(status[CommandParam.AxisParameter.TargetPosition] - status[CommandParam.AxisParameter.ActualPosition]) /
                    (SpeedFactor * status[CommandParam.AxisParameter.MaximumSpeed]),
                    250);

                while (!await IsTargetPositionReachedAsync(motorOrBank))
                {
                    token.ThrowIfCancellationRequested();
                    if (timeOut != default && (DateTime.Now - startTime) > timeOut)
                        throw new TimeoutException();
                    await Task.Delay(delayMs, token);
                    current = await GetActualPositionAsync(motorOrBank);
                    progressReporter?.Report((current, target));
                }

            }

            current = await GetActualPositionAsync(motorOrBank);
            progressReporter?.Report((current, target));

        }
        public virtual async Task ReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
        {
            token.ThrowIfCancellationRequested();

            var reply = await SendCommandAsync(
                Command.MoveToPosition,
                0, CommandParam.MoveType.Absolute,
                motorOrBank);
            if (!reply.IsSuccess)
                throw new InvalidOperationException("Failed to return to the origin.");

            token.ThrowIfCancellationRequested();

            await WaitForPositionReachedAsync(token);
        }

        public virtual async Task ReferenceReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
        {
            var reply = await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Start, motorOrBank);
            if (!reply.IsSuccess)
                throw new InvalidOperationException("Failed to start reference search.");

            if (token.IsCancellationRequested)
            {
                await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Stop, motorOrBank);
                token.ThrowIfCancellationRequested();
            }
            var deltaMs = 200;

            while ((reply = await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Status, motorOrBank))
                   .IsSuccess
                   && reply.ReturnValue != 0)
            {
                if (token.IsCancellationRequested)
                {
                    await SendCommandAsync(Command.ReferenceSearch, 0, CommandParam.RefSearchType.Stop, motorOrBank);
                    token.ThrowIfCancellationRequested();
                }
                await Task.Delay(deltaMs, token);
            }
        }

        public abstract Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(MotorBank motorOrBank = default);
        public abstract Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(MotorBank motorOrBank = default);



        public virtual void Dispose() { }
        public virtual ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        } 
    }
}