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
using System.Collections.Immutable;
using System.IO.Ports;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global

namespace StepMotor
{
    public class StepMotorFactory : IAsyncMotorFactory
    {
        public async Task<ImmutableList<Address>> FindDeviceAsync(SerialPort port,
            Address? startAddress = null, Address? endAddress = null)
        {
            startAddress ??= Address.DefaultStart;
            endAddress ??= Address.DefaultEnd;

            var result = ImmutableList.CreateBuilder<Address>();

            for (var address = startAddress.Value.RawValue; address <= endAddress.Value.RawValue; address++)
            {
                var motor = new StepMotorHandler(port, address);
                try
                {
                    if (await motor.PokeAddressInBinary())
                        result.Add(address);
                    else if (await motor.TrySwitchToBinary() && await motor.PokeAddressInBinary())
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

        public async Task<IAsyncMotor> TryCreateFromAddressAsync(
            SerialPort port, Address address, TimeSpan defaultTimeOut = default)
        {
            if (port is null)
                throw new ArgumentNullException(nameof(port));

            var motor = new StepMotorHandler(port, address, defaultTimeOut: defaultTimeOut);

            try
            {
                if (await motor.PokeAddressInBinary())
                    return motor;

                if (await motor.TrySwitchToBinary() && await motor.PokeAddressInBinary())
                    return motor;

            }
            catch (Exception)
            {
                // Ignored
            }

            motor.Dispose();
            return null;
        }

        public async Task<IAsyncMotor> TryCreateFirstAsync(
            SerialPort port, Address? startAddress = null, Address? endAddress = null,
            TimeSpan defaultTimeOut = default)
        {
            startAddress ??= Address.DefaultStart;
            endAddress ??= Address.DefaultEnd;

            if (port is null)
                throw new ArgumentNullException(nameof(port));
            if (startAddress > endAddress)
                throw new ArgumentOutOfRangeException(
                    $"[{nameof(startAddress)}] should be less than or equal to [{nameof(endAddress)}]");

            for (var address = startAddress.Value.RawValue; address <= endAddress.Value.RawValue; address++)
            {
                var motor = await TryCreateFromAddressAsync(port, address, defaultTimeOut);
                if (motor != null)
                    return motor;
            }

            return null;
        }

        public async Task<IAsyncMotor> CreateFirstOrFromAddressAsync(
            SerialPort port, Address address,
            Address? startAddress = null, Address? endAddress = null,
            TimeSpan defaultTimeOut = default)
            => (await TryCreateFromAddressAsync(port, address, defaultTimeOut)
                ?? await TryCreateFirstAsync(port, startAddress, endAddress, defaultTimeOut))
               ?? throw new InvalidOperationException("Failed to connect to step motor.");
    }
}