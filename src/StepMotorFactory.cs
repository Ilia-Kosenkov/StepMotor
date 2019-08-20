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
            public async Task<ImmutableList<byte>> FindDevice(SerialPort port, byte startAddress = 1,
                byte endAddress = 16)
            {

                var result = ImmutableList.CreateBuilder<byte>();

                for (var address = startAddress; address <= endAddress; address++)
                {
                    var motor = new StepMotorHandler(port, address);
                    try
                    {
                        if (await motor.PokeAddressInBinary(address))
                            result.Add(address);
                        else if (await motor.SwitchToBinary(address) && await motor.PokeAddressInBinary(address))
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

            public async Task<IAsyncMotor> TryCreateFromAddress(
                SerialPort port, byte address, TimeSpan defaultTimeOut = default)
            {
                if (port is null)
                    throw new ArgumentNullException(nameof(port));

                var motor = new StepMotorHandler(port, address, defaultTimeOut);

                try
                {
                    if (await motor.PokeAddressInBinary(address))
                        return motor;

                    if (await motor.SwitchToBinary(address) && await motor.PokeAddressInBinary(address))
                        return motor;

                }
                catch (Exception)
                {
                    // Ignored
                }

                motor.Dispose();
                return null;
            }

            public async Task<IAsyncMotor> TryCreateFirst(
                SerialPort port, byte startAddress = 1, byte endAddress = 16, TimeSpan defaultTimeOut = default)
            {
                if (port is null)
                    throw new ArgumentNullException(nameof(port));
                if (startAddress > endAddress)
                    throw new ArgumentOutOfRangeException(
                        $"[{nameof(startAddress)}] should be less than or equal to [{nameof(endAddress)}]");

                for (var address = startAddress; address <= endAddress; address++)
                {
                    var motor = await TryCreateFromAddress(port, address, defaultTimeOut);
                    if (motor != null)
                        return motor;
                }

                return null;
            }

            public async Task<IAsyncMotor> CreateFirstOrFromAddress(
                SerialPort port, byte address,
                byte startAddress = 1, byte endAddress = 16,
                TimeSpan defaultTimeOut = default)
                => (await TryCreateFromAddress(port, address, defaultTimeOut)
                    ?? await TryCreateFirst(port, startAddress, endAddress, defaultTimeOut))
                   ?? throw new InvalidOperationException("Failed to connect to step motor.");
    }
}