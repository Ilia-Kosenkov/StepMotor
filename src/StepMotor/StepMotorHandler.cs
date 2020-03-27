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
    public abstract class StepMotor : IAsyncMotor
    {
        protected const int ResponseSizeInBytes = 9;
        protected const int SpeedFactor = 30;

        protected static readonly Regex Regex = new Regex(@"[a-z]([a-z])\s*(\d{1,3})\s*(.*)\r",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public event StepMotorEventHandler? DataReceived;
        public event StepMotorEventHandler? ErrorReceived;

        public Address Address { get; }
        protected readonly SerialPort _port;

        protected readonly TimeSpan _timeOut;

        protected internal StepMotor(SerialPort port, Address? address, TimeSpan defaultTimeOut = default)
        {
            address ??= Address.DefaultStart;

            _timeOut = defaultTimeOut == default
                ? TimeSpan.FromMilliseconds(300)
                : defaultTimeOut;

            Address = address.Value;
            _port = port; //new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            // Event listeners
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
                _timeOut);

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

        public abstract Task ReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default);
        public abstract Task ReferenceReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default);

        public abstract Task WaitForPositionReachedAsync(CancellationToken token = default, TimeSpan timeOut = default,
            MotorBank motorOrBank = default);

        public abstract Task WaitForPositionReachedAsync(IProgress<(int Current, int Target)> progressReporter, CancellationToken token = default,
            TimeSpan timeOut = default, MotorBank motorOrBank = default);

        public abstract Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(MotorBank motorOrBank = default);
        public abstract Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(MotorBank motorOrBank = default);



        public abstract void Dispose();
        public abstract ValueTask DisposeAsync();
    }

    public sealed class StepMotorHandler : StepMotor
    {
        private readonly ConcurrentQueue<TaskCompletionSource<Reply>> _responseWaitQueue
            = new ConcurrentQueue<TaskCompletionSource<Reply>>();

        /// <summary>
        /// Used to suppress public events while performing WaitResponse.
        /// </summary>
        private volatile bool _suppressEvents;

        //public string PortName => _port.PortName;

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
            _port.DataReceived += Port_DataReceived;
            _port.ErrorReceived += Port_ErrorReceived;
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
                    address, 0, _timeOut);

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
                _port.WriteLine("");
                await Task.Delay(_timeOut);

                var addrStr = ((char)(address - 1 + 'A')).ToString();

                var command = $"{addrStr} BIN";
                var taskSrc = new TaskCompletionSource<Reply>();
                _responseWaitQueue.Enqueue(taskSrc);
                _port.WriteLine(command);
                try
                {
                    await WaitTimeOut(taskSrc.Task, _timeOut);
                }
                catch (StepMotorException exception)
                {
                    if (exception.RawData != null && exception.RawData.Length > 0)
                    {
                        var result = Regex.Match(_port.Encoding.GetString(exception.RawData));

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



        /// <summary>
        /// Handles internal COM port ErrorReceived.
        /// </summary>
        /// <param name="sender">COM port.</param>
        /// <param name="e">Event arguments.</param>
        

        /// <summary>
        /// Handles internal COM port DataReceived.
        /// </summary>
        /// <param name="sender">COM port.</param>
        /// <param name="e">Event arguments.</param>
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_port.BytesToRead < ResponseSizeInBytes) return;

            var len = _port.BytesToRead;
            
            TaskCompletionSource<Reply> taskSrc;

            while (_responseWaitQueue.TryDequeue(out taskSrc) &&
                   taskSrc.Task.IsCanceled)
            {
            }

            byte[]? pool = null;
            try
            {
                pool = ArrayPool<byte>.Shared.Rent(Math.Max(ResponseSizeInBytes, len));
                _port.Read(pool, 0, len);

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

            if (_port.BytesToRead > 0)
                buffer = new byte[_port.BytesToRead];

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
            _port.Write(toSend, 0, toSend.Length);

            // Wait for response
            return await WaitResponseAsync(responseTaskSource.Task, command, timeOut);
        }




        /// <summary>
        /// Implements interface and frees resources
        /// </summary>
        public override void Dispose()
        {
            while (_responseWaitQueue.TryDequeue(out var taskSrc) && taskSrc?.Task.IsCanceled == false)
                taskSrc.SetException(new ObjectDisposedException(nameof(StepMotorHandler)));
            _port.DataReceived -= Port_DataReceived;
            _port.ErrorReceived -= Port_ErrorReceived;
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        /// <summary>
        /// Queries status of all Axis parameters.
        /// </summary>
        /// <param name="motorOrBank">Motor or bank, defaults to 0.</param>
        /// <returns>Retrieved values for each AxisParameter queried.</returns>
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



        /// <summary>
        /// Queries status of essential Axis parameters.
        /// </summary>
        /// <param name="motorOrBank">Motor or bank, defaults to 0.</param>
        /// <returns>Retrieved values for each AxisParameter queried.</returns>
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

       


        public override async Task WaitForPositionReachedAsync(CancellationToken token = default, TimeSpan timeOut = default, MotorBank motorOrBank = default)
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

        public override async Task WaitForPositionReachedAsync(
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




        public override async Task ReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
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

        public override async Task ReferenceReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default)
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

