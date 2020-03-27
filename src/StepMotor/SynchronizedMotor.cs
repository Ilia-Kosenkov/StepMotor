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
using System.Threading;
using System.Threading.Tasks;

namespace StepMotor
{
    public class SynchronizedMotor : StepMotor
    {
        private readonly byte[] _commandBuffer = new byte[ResponseSizeInBytes];
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<Reply>? _taskSource;

        internal SynchronizedMotor(SerialPort port, Address? address, TimeSpan defaultTimeOut = default)
            : base(port, address, defaultTimeOut)
        {
            // Event listeners
            // Not sure if needed
            Port.DataReceived += Port_DataReceived;
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!(sender is SerialPort port))
                return;

            if (port.BytesToRead < ResponseSizeInBytes)
                return;

            if (port.BytesToRead > ResponseSizeInBytes)
            {
                // This should not happen as step motors through out 
                // `ResponseSizeInBytes` bytes after each command.
                // If response is larger, something went wrong, or interfering,
                // or there is an unexpected race condition.
                // Rare case, no need for optimization
                var buff = new byte[port.BytesToRead];
                port.Read(buff, 0, port.BytesToRead);
                _taskSource?.SetException(new StepMotorException("Unexpected content in the motor's buffer.", buff));
                return;
            }

            var span = _commandBuffer.AsSpan();

            span.Fill(0);
            port.Read(_commandBuffer, 0, ResponseSizeInBytes);
            var reply = new Reply(_commandBuffer);
            span.Fill(0);
            _taskSource?.SetResult(reply);
        }

        public override async Task<Reply> SendCommandAsync(
            Command command, int argument, byte type,
            Address address, MotorBank motorOrBank,
            TimeSpan timeOut, CancellationToken token = default)
        {
            static void FillInBytes(byte address, byte command, byte type, byte motorOrBank, int arg, Span<byte> buff)
            {
                Debug.Assert(buff.Length == ResponseSizeInBytes, "Command buffer is of incorrect size");
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
                Port.Write(_commandBuffer, 0, _commandBuffer.Length);
                //await Task.Delay(TimeOut);
                await Task.Yield();
                return await ForTask(_taskSource.Task, timeOut, token);
            }
            finally
            {
                _taskSource = null;
                _mutex.Release();
            }

            // Wait for response
            //return await WaitResponseAsync(responseTaskSource.Task, command, timeOut);
        }



        public override async Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(
            MotorBank motorOrBank = default)
        {
            var builder = ImmutableDictionary.CreateBuilder<CommandParam.AxisParameter, int>();

            // For each basic Axis Parameter queries its value
            // Uses explicit conversion of byte to AxisParameter
            foreach (var param in CommandParam.GeneralAxisParams)
            {
                try
                {
                    var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank);
                    if (reply.IsSuccess)
                        builder.Add(param, reply.ReturnValue);
                }
                catch (Exception)
                {
                    // ignore
                }
            }
            return builder.ToImmutable();
        }

        public override async Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(
            MotorBank motorOrBank = default)
        {
            var builder = ImmutableDictionary.CreateBuilder<CommandParam.AxisParameter, int>();

            // For each basic Axis Parameter queries its value
            // Uses explicit conversion of byte to AxisParameter
            foreach (var param in CommandParam.RotationAxisParams)
            {
                try
                {
                    var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank);
                    if (reply.IsSuccess)
                        builder.Add(param, reply.ReturnValue);
                }
                catch (Exception)
                {
                    // ignore
                }
            }
            return builder.ToImmutable();
        }


        public  override void Dispose()
        {
            Port.DataReceived -= Port_DataReceived;
            base.Dispose();
        }

        public override async Task<bool> TrySwitchToBinary()
        {
            Port.WriteLine("");
            await Task.Delay(TimeOut);

            var addrStr = ((char)(Address - 1 + 'A'));
            var command = $"{addrStr} BIN";
            await _mutex.WaitAsync();
            try
            {
                _taskSource = new TaskCompletionSource<Reply>();
                Port.DiscardOutBuffer();
                Port.DiscardInBuffer();
                Port.WriteLine(command);
                if ((await ForTask(_taskSource.Task, TimeOut, default)).IsSuccess)
                    return true;
            }
            catch (StepMotorException smEx)
            {
                if (smEx.RawData?.Length > 0 && CheckIfAsciiResponse(
                        command.AsSpan(),
                        Port.Encoding.GetString(smEx.RawData).AsSpan(),
                        addrStr,
                        Port.NewLine.AsSpan()))
                    return true;

            }
            finally
            {
                _taskSource = null;
                Port.DiscardOutBuffer();
                Port.DiscardInBuffer();
                _mutex.Release();
            }

            return false;
        }

        private async Task<T> ForTask<T>(Task<T> task, TimeSpan timeOut, CancellationToken token)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));

            // Remove
            Console.WriteLine(timeOut);

            if (timeOut == default && token == default)
                return await task;

            var controller = Task.Delay(timeOut, token);

            if (await Task.WhenAny(task, controller) == task)
                return await task;
                
            token.ThrowIfCancellationRequested();
            throw new TimeoutException();
        }

        private static bool CheckIfAsciiResponse(
            ReadOnlySpan<char> command,
            ReadOnlySpan<char> response,
            char address,
            ReadOnlySpan<char> newLine)
        {
            // Response is too short
            if (response.Length < command.Length + newLine.Length + 4)
                return false;

            // First part should be equal to command
            if (!response.Slice(0, command.Length).SequenceEqual(command))
                return false;

            // Then new line
            if (!response.Slice(command.Length, newLine.Length).SequenceEqual(newLine))
                return false;

            var actualResponse = response.Slice(command.Length + newLine.Length);
            
            // Then address of the host and address of the motor, like `BA`
            // If it does not match, then response is incorrect
            return char.IsLetter(actualResponse[0]) && actualResponse[1] == address;
        }
    }
}