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
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace StepMotor
{
    public class SynchronizedMotor : IAsyncMotor
    {
        private const int ResponseSizeInBytes = 9;

        private const int SpeedFactor = 30;

        private readonly SerialPort _port;
        private readonly TimeSpan _timeOut;
        private readonly byte[] _commandBuffer = new byte[ResponseSizeInBytes];
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);
        private TaskCompletionSource<Reply> _taskSource;
        public Address Address { get; }
        public event StepMotorEventHandler? DataReceived;
        public event StepMotorEventHandler? ErrorReceived;

        public SynchronizedMotor(SerialPort port, Address address, TimeSpan defaultTimeOut = default)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            Address = address;
            _timeOut = defaultTimeOut == default
                ? TimeSpan.FromMilliseconds(300)
                : defaultTimeOut;

            // Event listeners
            // Not sure if needed
            _port.DataReceived += Port_DataReceived;
            //_port.ErrorReceived += Port_ErrorReceived;
            _port.NewLine = "\r";

            if (!_port.IsOpen)
            {
                // Creates port
                _port.BaudRate = 9600;
                _port.Parity = Parity.None;
                _port.DataBits = 8;
                _port.StopBits = StopBits.One;
                _port.Open();
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!(sender is SerialPort port))
                return;

            if (port.BytesToRead < ResponseSizeInBytes)
                return;

            var span = _commandBuffer.AsSpan();

            span.Fill(0);
            port.Read(_commandBuffer, 0, ResponseSizeInBytes);
            var reply = new Reply(_commandBuffer);
            _taskSource?.SetResult(reply);
            span.Fill(0);

            // This should not happen as step motors through out 
            // `ResponseSizeInBytes` bytes after each command.
            // If response is larger, something went wrong, or interfering,
            // or there is an unexpected race condition.
            if (port.BytesToRead > 0)
            {
                // Rare case, no need for optimization
                var buff = new byte[port.BytesToRead];
                port.Read(buff, 0, port.BytesToRead);
                throw new StepMotorException("Unexpected content in the motor's buffer.", buff);
            }
        }

        private async Task<Reply> SendCommandAsync(
            Command command, int argument, byte type,
            Address address, MotorBank motorOrBank, 
            TimeSpan timeOut, CancellationToken token = default)
        {
            static void FillInBytes(byte address, byte command, byte type, byte motorOrBank, int arg, Span<byte> buff)
            {
                Debug.Assert(buff.Length != ResponseSizeInBytes, "Command buffer is of incorrect size");
                buff.Fill(0);
                buff[0] = address;
                buff[1] = command;
                buff[2] = type;
                buff[3] = motorOrBank;
                BinaryPrimitives.WriteInt32BigEndian(buff.Slice(4), arg);

                byte sum = 0;
                unchecked 
                {
                    foreach (var item in buff.Slice(0, ResponseSizeInBytes - 1))
                        sum += item;
                }
                buff[ResponseSizeInBytes - 1] = sum;
            }

            if (!await _mutex.WaitAsync(timeOut, token))
            {
                token.ThrowIfCancellationRequested();
                throw new TimeoutException();
            }

            try
            {
                // Constructs raw command array
                FillInBytes(address, (byte) command, type, motorOrBank, argument, _commandBuffer);
                _taskSource = new TaskCompletionSource<Reply>();
                // Sends data to COM port
                _port.Write(_commandBuffer, 0, _commandBuffer.Length);
                return await ForTask(_taskSource.Task, timeOut, token);
            }
            finally
            {
                _mutex.Release();
            }

            // Wait for response
            //return await WaitResponseAsync(responseTaskSource.Task, command, timeOut);
        }

        public Task ReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task ReferenceReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task WaitForPositionReachedAsync(CancellationToken token = default, TimeSpan timeOut = default,
            MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task WaitForPositionReachedAsync(IProgress<(int Current, int Target)> progressReporter, CancellationToken token = default,
            TimeSpan timeOut = default, MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsTargetPositionReachedAsync(MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetActualPositionAsync(MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task<Reply> MoveToPosition(int position, CommandParam.MoveType rotationType = CommandParam.MoveType.Absolute,
            MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetAxisParameterAsync(CommandParam.AxisParameter param, MotorBank motorOrBank = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private static async Task<T> ForTask<T>(Task<T> task, TimeSpan timeOut, CancellationToken token)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            if (timeOut == default)
                timeOut = TimeSpan.MaxValue;
            var result = await Task.WhenAny(task, Task.Delay(timeOut, token));

            if (result == task)
                return await task;
                
            token.ThrowIfCancellationRequested();
            throw new TimeoutException();
        }
    }
}