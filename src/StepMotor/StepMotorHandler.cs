//     MIT License
//     
//     Copyright(c) 2018-2019 Ilia Kosenkov
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
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global

namespace StepMotor
{
    public sealed class StepMotorHandler : StepMotor
    {
        protected static readonly Regex Regex = new Regex(@"[a-z]([a-z])\s*(\d{1,3})\s*(.*)\r",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly ConcurrentQueue<TaskCompletionSource<Reply>> _responseWaitQueue
            = new ConcurrentQueue<TaskCompletionSource<Reply>>();

        /// <summary>
        /// Used to suppress public events while performing WaitResponse.
        /// </summary>
        private volatile bool _suppressEvents;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="port">COM port.</param>
        /// <param name="defaultTimeOut">Default response timeout.</param>
        /// <param name="address">Device address.</param>
        /// <exception cref="ArgumentOutOfRangeException"/>
        internal StepMotorHandler(SerialPort port, Address? address, TimeSpan defaultTimeOut = default)
            :base(port, address, defaultTimeOut)
        {
            // Event listeners
            Port.DataReceived += Port_DataReceived;
            Port.ErrorReceived += Port_ErrorReceived;
        }


        internal async Task<bool> PokeAddressInBinary(Address address)
        {
            var oldStatus = _suppressEvents;
            try
            {
                _suppressEvents = true;

                var result = await SendCommandAsync(
                    Command.GetAxisParameter,
                    1, CommandParam.Default,
                    address, 0, TimeOut);

                return result.IsSuccess;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _suppressEvents = oldStatus;
            }

        }

        internal async Task<bool> SwitchToBinary(Address address)
        {
            var oldStatus = _suppressEvents;

            try
            {
                _suppressEvents = true;
                Port.WriteLine("");
                await Task.Delay(TimeOut);

                var addrStr = ((char)(address - 1 + 'A')).ToString();

                var command = $"{addrStr} BIN";
                var taskSrc = new TaskCompletionSource<Reply>();
                _responseWaitQueue.Enqueue(taskSrc);
                Port.WriteLine(command);
                try
                {
                    await WaitTimeOut(taskSrc.Task, TimeOut);
                }
                catch (StepMotorException exception)
                {
                    if (exception.RawData != null && exception.RawData.Length > 0)
                    {
                        var result = Regex.Match(Port.Encoding.GetString(exception.RawData));

                        if (result.Groups.Count == 4
                            && result.Groups[1].Value == addrStr
                            && result.Groups[2].Value == "100")
                            return true;
                    }

                }
                catch (TimeoutException)
                {
                    taskSrc.SetCanceled();
                }

                return false;
            }
            finally
            {
                _suppressEvents = oldStatus;
            }

        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (Port.BytesToRead < ResponseSizeInBytes) return;

            var len = Port.BytesToRead;
            
            TaskCompletionSource<Reply> taskSrc;

            while (_responseWaitQueue.TryDequeue(out taskSrc) &&
                   taskSrc.Task.IsCanceled)
            {
            }

            byte[]? pool = null;
            try
            {
                pool = ArrayPool<byte>.Shared.Rent(Math.Max(ResponseSizeInBytes, len));
                Port.Read(pool, 0, len);

                if (len == ResponseSizeInBytes)
                {
                    var reply = new Reply(pool.AsSpan(0, ResponseSizeInBytes));
                    taskSrc?.SetResult(reply);
                    OnDataReceived(new StepMotorEventArgs(pool.AsSpan(0, ResponseSizeInBytes).ToArray()));
                }
                else if (len % ResponseSizeInBytes == 0)
                {
                    var reply = new Reply(pool.AsSpan(0, ResponseSizeInBytes));
                    taskSrc?.SetResult(reply);
                    OnDataReceived(new StepMotorEventArgs(pool.AsSpan(0, ResponseSizeInBytes).ToArray()));

                    for (var i = 0; i < len / ResponseSizeInBytes; i++)
                    {
                        reply = new Reply(pool.AsSpan(0, ResponseSizeInBytes));
                        while (_responseWaitQueue.TryDequeue(out taskSrc) && taskSrc.Task.IsCanceled)
                        {
                        }

                        taskSrc?.SetResult(reply);
                        OnDataReceived(new StepMotorEventArgs(pool.AsSpan(0, ResponseSizeInBytes).ToArray()));
                    }
                }
                else
                {
                    taskSrc?.SetException(new StepMotorException("Step motor response is inconsistent", pool));
                    OnDataReceived(new StepMotorEventArgs(pool.ToArray()));
                }
            }
            finally
            {
                if (pool != null)
                    ArrayPool<byte>.Shared.Return(pool, true);
            }
        }

        private void Port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Reads last response
            byte[]? buffer = null;

            if (Port.BytesToRead > 0)
                buffer = new byte[Port.BytesToRead];

            while (_responseWaitQueue.TryDequeue(out var taskSrc))
                taskSrc.SetException(new InvalidOperationException(@"Step motor returned an error."));

            OnErrorReceived(new StepMotorEventArgs(buffer ?? Array.Empty<byte>()));
        }


        public override async Task<Reply> SendCommandAsync(
            Command command, int argument, byte type,
            Address address, MotorBank motorOrBank, 
            TimeSpan timeOut, CancellationToken token = default)
        {

            // Converts Int32 into byte array. 
            // Motor accepts Most Significant Bit First, so for LittleEndian 
            // array should be reversed.
            var val = BitConverter.GetBytes(argument);
            // Constructs raw command array
            byte[] toSend;
            if (BitConverter.IsLittleEndian)
            {
                toSend = new byte[]
                {
                    address,
                    (byte) command,
                    type,
                    motorOrBank,
                    val[3],
                    val[2],
                    val[1],
                    val[0],
                    0 // Reserved for checksum
                };
            }
            else
            {
                toSend = new byte[]
                {
                    address,
                    (byte) command,
                    type,
                    motorOrBank,
                    val[0],
                    val[1],
                    val[2],
                    val[3],
                    0 // Reserved for checksum
                };
            }

            byte sum = 0;
            unchecked
            {
                for (var i = 0; i < toSend.Length - 1; i++)
                    sum += toSend[i];
            }

            // Takes least significant byte
            toSend[8] = sum;

            var responseTaskSource = new TaskCompletionSource<Reply>();
            _responseWaitQueue.Enqueue(responseTaskSource);
            // Sends data to COM port
            Port.Write(toSend, 0, toSend.Length);

            // Wait for response
            return await WaitResponseAsync(responseTaskSource.Task, command, timeOut);
        }

        public override void Dispose()
        {
            while (_responseWaitQueue.TryDequeue(out var taskSrc) && taskSrc?.Task.IsCanceled == false)
                taskSrc.SetException(new ObjectDisposedException(nameof(StepMotorHandler)));
            Port.DataReceived -= Port_DataReceived;
            Port.ErrorReceived -= Port_ErrorReceived;
            base.Dispose();
        }
        
        public override async Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(MotorBank motorOrBank = default)
        {
            // Stores old state
            var oldState = _suppressEvents;
            _suppressEvents = true;

            var status = ImmutableDictionary.CreateBuilder<CommandParam.AxisParameter, int>();

            // Ensures state is restored
            try
            {

                // For each basic Axis Parameter queries its value
                // Uses explicit conversion of byte to AxisParameter
                foreach (var param in CommandParam.GeneralAxisParams)
                {
                    var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank);
                    if (reply.IsSuccess)
                        status.Add(param, reply.ReturnValue);
                }
            }
            finally
            {
                // Restores state
                _suppressEvents = oldState;
            }

            // Returns query result
            return status.ToImmutable();
        }


        public override async Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(
            MotorBank motorOrBank = default)
        {
            // Stores old state
            var oldState = _suppressEvents;
            _suppressEvents = true;

            var status = ImmutableDictionary.CreateBuilder<CommandParam.AxisParameter, int>();

            // Ensures state is restored
            try
            {

                foreach (var param in CommandParam.RotationAxisParams)
                {
                    var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank);
                    if (reply.IsSuccess)
                        status.Add(param, reply.ReturnValue);
                }

            }
            finally
            {
                // Restores state
                _suppressEvents = oldState;
            }

            // Returns query result
            return status.ToImmutable();
        }

        
        private static Task WaitTimeOut(Task task, TimeSpan timeOut = default, CancellationToken token = default)
        {
            if (timeOut == default)
                return task;
            var src = CancellationTokenSource.CreateLinkedTokenSource(token);

            var result = Task.WaitAll(
                new[]
                {
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            src.Cancel();
                    }, src.Token)
                }, (int)timeOut.TotalMilliseconds, src.Token);

            if (result)
                return task;

            if (src.IsCancellationRequested && task.IsFaulted)
                return task;
            return src.IsCancellationRequested ? Task.FromCanceled(src.Token) : Task.FromException<TimeoutException>(new TimeoutException());
        }
        private static async Task<Reply> WaitResponseAsync(Task<Reply> source, Command command, TimeSpan timeOut = default)
        {
            if (source is null)
                throw new NullReferenceException(nameof(source));
            if (timeOut == default)
                return await source;

            //try
            //{
            await WaitTimeOut(source, timeOut);

            if (source.IsCompleted)
            {
                var result = await source;
                if (result.Command == command)
                    return result;
                throw new InvalidOperationException(@"Sent/received command mismatch.");
            }
            //}
            //// WATCH : no exception handling
            //catch (Exception e)
            //{

            //}
            throw new TimeoutException();

        }
    }
}

