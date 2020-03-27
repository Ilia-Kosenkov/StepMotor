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
using System.Collections.Immutable;
using System.IO.Ports;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace StepMotor
{
    public interface IAsyncMotorFactory
    {
        Task<ImmutableList<Address>> FindDeviceAsync(SerialPort port, Address? startAddress = null, Address? endAddress = null);

        Task<IAsyncMotor?> TryCreateFromAddressAsync(
            SerialPort port, Address address, TimeSpan defaultTimeOut = default);

        Task<IAsyncMotor?> TryCreateFirstAsync(
            SerialPort port, Address? startAddress = null, Address? endAddress = null, TimeSpan defaultTimeOut = default);

        Task<IAsyncMotor> CreateFirstOrFromAddressAsync(
            SerialPort port, Address address,
            Address? startAddress = null, Address? endAddress = null,
            TimeSpan defaultTimeOut = default);
    }
}