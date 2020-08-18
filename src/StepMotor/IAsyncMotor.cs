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
using System.Threading;
using System.Threading.Tasks;

namespace StepMotor
{
    public interface IAsyncMotor : IDisposable, IAsyncDisposable
    {
        Address Address { get; }

        Task ReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default);
        Task ReferenceReturnToOriginAsync(CancellationToken token = default, MotorBank motorOrBank = default);

        Task WaitForPositionReachedAsync(CancellationToken token = default, TimeSpan timeOut = default, MotorBank motorOrBank = default);

        Task WaitForPositionReachedAsync(IProgress<(int Current, int Target)> progressReporter,
            CancellationToken token = default, TimeSpan timeOut = default, MotorBank motorOrBank = default);

        //Task<bool> IsTargetPositionReachedAsync(MotorBank motorOrBank = default);

        //Task<int> GetPositionAsync(MotorBank motorOrBank = default);

        Task<int> GetActualPositionAsync(MotorBank motorOrBank = default);

        Task<ImmutableDictionary<CommandParam.AxisParameter, int>> GetRotationStatusAsync(MotorBank motorOrBank = default);

        

        Task<int> GetAxisParameterAsync(CommandParam.AxisParameter param, MotorBank motorOrBank = default);

        Task<bool> TrySwitchToBinary();

        Task<Reply> SendCommandAsync(
            Command command, int argument,
            CommandParam param, MotorBank motorOrBank = default);

        Task<int> InvokeCommandAsync(
            Command command,
            int argument,
            CommandParam param,
            MotorBank motorOrBank = default);

        Task<bool> IsInMotionAsync(MotorBank motorOrBank = default);
        Task StopAsync(MotorBank motorOrBank = default);
    }
}