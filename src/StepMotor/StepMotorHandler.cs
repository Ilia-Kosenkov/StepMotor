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
    public class StepMotorHandler : IAsyncMotor
    {
        private static readonly Regex Regex = new Regex(@"[a-z]([a-z])\s*(\d{1,3})\s*(.*)\r",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private const int ResponseSizeInBytes = 9;

        private const int SpeedFactor = 30;

        /// <summary>
        /// Delegate that handles Data/Error received events.
        /// </summary>
        /// <param name="sender">Sender is <see cref="StepMotorHandler"/>.</param>
        /// <param name="e">Event arguments.</param>
        public delegate void StepMotorEventHandler(object sender, StepMotorEventArgs e);

        /// <summary>
        /// Backend serial port 
        /// </summary>
        private readonly SerialPort _port;

        private readonly TimeSpan _timeOut;

        private readonly ConcurrentQueue<TaskCompletionSource<Reply>> _responseWaitQueue
            = new ConcurrentQueue<TaskCompletionSource<Reply>>();

        /// <summary>
        /// Used to suppress public events while performing WaitResponse.
        /// </summary>
        private volatile bool _suppressEvents;

        //public string PortName => _port.PortName;
        public byte Address { get; }

        /// <summary>
        /// Fires when data has been received from COM port.
        /// </summary>
        public event StepMotorEventHandler DataReceived;
        /// <summary>
        /// Fires when error data has been received from COM port.
        /// </summary>
        public event StepMotorEventHandler ErrorReceived;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="port">COM port.</param>
        /// <param name="defaultTimeOut">Default response timeout.</param>
        /// <param name="address">Device address.</param>
        /// <exception cref="ArgumentOutOfRangeException"/>
        internal StepMotorHandler(SerialPort port, byte address = 1, TimeSpan defaultTimeOut = default)
        {
            _timeOut = defaultTimeOut == default
                ? TimeSpan.FromMilliseconds(300)
                : defaultTimeOut;

            Address = address;
            _port = port; //new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
            // Event listeners
            _port.DataReceived += OnPortDataReceived;
            _port.ErrorReceived += OnPortErrorReceived;
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


        internal async Task<bool> PokeAddressInBinary(byte address)
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

        internal async Task<bool> SwitchToBinary(byte address)
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
        protected void OnPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Reads last response
            byte[] buffer = null;

            if (_port.BytesToRead > 0)
                buffer = new byte[_port.BytesToRead];

            while (_responseWaitQueue.TryDequeue(out var taskSrc))
                taskSrc.SetException(new InvalidOperationException(@"Step motor returned an error."));

            OnErrorReceived(new StepMotorEventArgs(buffer ?? Array.Empty<byte>()));
        }

        /// <summary>
        /// Handles internal COM port DataReceived.
        /// </summary>
        /// <param name="sender">COM port.</param>
        /// <param name="e">Event arguments.</param>
        protected void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_port.BytesToRead <= 0) return;

            var len = _port.BytesToRead;
            byte[] pool = null;
            TaskCompletionSource<Reply> taskSrc;

            while (_responseWaitQueue.TryDequeue(out taskSrc) &&
                   taskSrc.Task.IsCanceled)
            {
            }

            // Sometimes step motor writes checksum with a delay.
            // On trigger .BytesToRead == 8
            // Checksum is usually written immediately after, 
            // so small delay allows to capture it
            if (len < ResponseSizeInBytes)
            {
                Task.Delay(TimeSpan.FromMilliseconds(_timeOut.TotalMilliseconds / 4)).GetAwaiter().GetResult();
                if (_port.BytesToRead % ResponseSizeInBytes == 0)
                {
                    len = _port.BytesToRead;

                }
            }

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
                    taskSrc.SetResult(reply);
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

        /// <summary>
        /// Used to fire DataReceived event.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnDataReceived(StepMotorEventArgs e)
        {
            if (!_suppressEvents)
                DataReceived?.Invoke(this, e);
        }
        /// <summary>
        /// Used to fire ErrorReceived event.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnErrorReceived(StepMotorEventArgs e)
        {
            if (!_suppressEvents)
                ErrorReceived?.Invoke(this, e);
        }


        protected async Task<Reply> SendCommandAsync(Command command, int argument,
            byte type,
            byte address,
            byte motorOrBank, TimeSpan timeOut)
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

        public Task<Reply> SendCommandAsync(
            Command command, int argument,
            CommandParam param, byte motorOrBank = 0) =>
            SendCommandAsync(
                command,
                argument,
                param,
                Address,
                motorOrBank,
                _timeOut);


        /// <summary>
        /// Implements interface and frees resources
        /// </summary>
        public void Dispose()
        {
            while (_responseWaitQueue.TryDequeue(out var taskSrc) && taskSrc?.Task.IsCanceled == false)
                taskSrc.SetException(new ObjectDisposedException(nameof(StepMotorHandler)));
        }

        /// <summary>
        /// Queries status of all Axis parameters.
        /// </summary>
        /// <param name="motorOrBank">Motor or bank, defaults to 0.</param>
        /// <returns>Retrieved values for each AxisParameter queried.</returns>
        public async Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetStatusAsync(
            byte motorOrBank = 0)
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
                    var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param);
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

        public Task<Reply> MoveToPosition(int position,
            CommandParam.MoveType type = CommandParam.MoveType.Absolute,
            byte motorOrBank = 0) =>
            SendCommandAsync(Command.MoveToPosition, position, type, motorOrBank);


        /// <summary>
        /// Queries status of essential Axis parameters.
        /// </summary>
        /// <param name="motorOrBank">Motor or bank, defaults to 0.</param>
        /// <returns>Retrieved values for each AxisParameter queried.</returns>
        public async Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(
            byte motorOrBank = 0)
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
                    var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param);
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

        public async Task<int> GetActualPositionAsync(byte motorOrBank = 0)
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

        public async Task<bool> IsTargetPositionReachedAsync(byte motorOrBank = 0)
        {
            var reply = await SendCommandAsync(
                Command.GetAxisParameter,
                0, CommandParam.AxisParameter.TargetPositionReached,
                motorOrBank);
            if (reply.IsSuccess)
                return reply.ReturnValue == 1;
            throw new InvalidOperationException("Failed to retrieve value.");
        }

        public async Task WaitForPositionReachedAsync(CancellationToken token = default, TimeSpan timeOut = default, byte motorOrBank = 0)
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

        public async Task WaitForPositionReachedAsync(
            IProgress<(int Current, int Target)> progressReporter,
            CancellationToken token = default,
            TimeSpan timeOut = default,
            byte motorOrBank = 0)
        {
            token.ThrowIfCancellationRequested();
            var startTime = DateTime.Now;

            var reply = await SendCommandAsync(
                Command.GetAxisParameter
                , 0,
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

        public async Task<bool> IsInMotionAsync(byte motorOrBank = 0)
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

        public async Task StopAsync(byte motorOrBank = 0)
        {
            var reply = await SendCommandAsync(
                Command.MotorStop,
                0,
                CommandParam.RefSearchType.Stop,
                motorOrBank: motorOrBank);
            if (!reply.IsSuccess)
                throw new InvalidOperationException("Failed to retrieve value.");
        }

        public async Task ReturnToOriginAsync(CancellationToken token = default, byte motorOrBank = 0)
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

        public async Task ReferenceReturnToOriginAsync(CancellationToken token = default, byte motorOrBank = 0)
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

        public async Task<int> GetAxisParameterAsync(CommandParam.AxisParameter param, byte motorOrBank = 0)
        {
            var reply = await SendCommandAsync(Command.GetAxisParameter, 0, param, motorOrBank);
            if (reply.IsSuccess)
                return reply.ReturnValue;
            throw new InvalidOperationException($"{nameof(Command.GetAxisParameter)} failed to retrieve parameter.");
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
            if (src.IsCancellationRequested)
                return Task.FromCanceled(src.Token);

            return Task.FromException<TimeoutException>(new TimeoutException());



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

        //public static async Task<ImmutableList<byte>> FindDeviceAsync(SerialPort port, byte startAddress = 1,
        //    byte endAddress = 16)
        //{

        //    var result = ImmutableList.CreateBuilder<byte>();

        //    for (var address = startAddress; address <= endAddress; address++)
        //    {
        //        var motor = new StepMotorHandler(port, address);
        //        try
        //        {
        //            if (await motor.PokeAddressInBinary(address))
        //                result.Add(address);
        //            else
        //            {
        //                await motor.SwitchToBinary(address);
        //                if (await motor.PokeAddressInBinary(address))
        //                    result.Add(address);
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            // Ignored
        //        }
        //        finally
        //        {
        //            motor.Dispose();
        //        }
        //    }

        //    return result.ToImmutable();
        //}

        //public static async Task<IAsyncMotor> TryCreateFromAddressAsync(
        //    SerialPort port, byte address, TimeSpan defaultTimeOut = default)
        //{
        //    if (port is null)
        //        throw new ArgumentNullException(nameof(port));

        //    var motor = new StepMotorHandler(port , address, defaultTimeOut);

        //    try
        //    {
        //        if (await motor.PokeAddressInBinary(address))
        //            return motor;

        //        await motor.SwitchToBinary(address);
        //        if (await motor.PokeAddressInBinary(address))
        //            return motor;

        //    }
        //    catch (Exception)
        //    {
        //        // Ignored
        //    }
        //    motor.Dispose();
        //    return null;
        //}

        //public static async Task<IAsyncMotor> TryCreateFirstAsync(
        //    SerialPort port, byte startAddress = 1, byte endAddress = 16, TimeSpan defaultTimeOut = default)
        //{
        //    if (port is null)
        //        throw new ArgumentNullException(nameof(port));
        //    if (startAddress > endAddress)
        //        throw new ArgumentOutOfRangeException(
        //            $"[{nameof(startAddress)}] should be less than or equal to [{nameof(endAddress)}]");

        //    for (var address = startAddress; address <= endAddress; address++)
        //    {
        //        var motor = await TryCreateFromAddressAsync(port, address, defaultTimeOut);
        //        if (motor != null)
        //            return motor;
        //    }

        //    return null;
        //}

        //public static async Task<IAsyncMotor> CreateFirstOrFromAddressAsync(
        //    SerialPort port, byte address,
        //    byte startAddress = 1, byte endAddress = 16,
        //    TimeSpan defaultTimeOut = default)
        //    => (await TryCreateFromAddressAsync(port, address, defaultTimeOut)
        //        ?? await TryCreateFirstAsync(port, startAddress, endAddress, defaultTimeOut))
        //       ?? throw new InvalidOperationException("Failed to connect to step motor.");
    }

}

