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
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace StepMotor
{
    public class StepMotorProvider<T> : IAsyncMotorFactory where T : StepMotor
    {
        protected ILogger? Logger;

        public StepMotorProvider()
        {
            ThrowIfTypeIsAbstract();
        }

        public StepMotorProvider(ILogger? logger)
        {
            ThrowIfTypeIsAbstract();
            Logger = logger;
        }

        private void ThrowIfTypeIsAbstract()
        {
            if(typeof(T).IsAbstract)
                throw new ArgumentException("Cannot provide an instance of an abstract class", $"{nameof(T)}");
        }

        public async Task<ImmutableList<Address>> FindDeviceAsync(
            SerialPort port, Address? startAddress = null, Address? endAddress = null)
        {
            startAddress ??= Address.DefaultStart;
            endAddress ??= Address.DefaultEnd;

            var result = ImmutableList.CreateBuilder<Address>();

            for (var address = startAddress.Value.RawValue; address <= endAddress.Value.RawValue; address++)
            {
                var motor = CreateMotor(port, address, default);
                try
                {
                    if(await CheckMotorStatus(motor))
                        result.Add(address);
                }
                catch (Exception)
                {
                    // Ignored
                }
                finally
                {
                    motor.Dispose();
                }
            }

            return result.ToImmutable();
        }

        public async Task<IAsyncMotor?> TryCreateFromAddressAsync(
            SerialPort port, Address address, TimeSpan defaultTimeOut = default)
        {
            _ = port ?? throw new ArgumentNullException(nameof(port));

            var motor = CreateMotor(port, address, defaultTimeOut);
            if (await CheckMotorStatus(motor))
            {
                await motor.ReturnToOriginAsync();
                return motor;
            }

            await motor.DisposeAsync();
            return null;
        }

        private static async Task<bool> CheckMotorStatus(IAsyncMotor motor)
        {
            try
            {
                if ((await motor.SendCommandAsync(Command.GetAxisParameter, 1, CommandParam.Default, 0)).IsSuccess)
                    return true;
            }
            catch (TimeoutException)
            {
                // Ignored
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                if (await motor.TrySwitchToBinary())
                    return true;
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }


        private IAsyncMotor CreateMotor(SerialPort port, Address? address, TimeSpan timeOut)
        {
            var args = new[] { typeof(SerialPort), typeof(Address?), typeof(ILogger), typeof(TimeSpan) };
            var ctor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, args, null);
            if (ctor is null)
                throw new InvalidOperationException($"Type {typeof(T).Name} does not have specific constructor.");

            return ctor.Invoke(new object[] {port, address!, Logger!, timeOut}) as IAsyncMotor
                   ?? throw new InvalidOperationException(
                       $"Type mismatch: {typeof(T).Name} does not implement {typeof(IAsyncMotor).Name}");
        }
    }
}