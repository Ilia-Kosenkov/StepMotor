//    This file is part of Dipol-3 Camera Manager.

//     MIT License
//     
//     Copyright(c) 2018 Ilia Kosenkov
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

using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using StepMotor;

namespace DebugTests
{
    [TestFixture]
    public class SimultaneousStepMotorTests
    {

        private SerialPort _port1;
        private SerialPort _port2;

        private IAsyncMotor _handler1;
        private IAsyncMotor _handler2;

        [SetUp]
        public async Task SetUp()
        {
            //var factory = new StepMotorFactory();
            var factory = new SynchronizedMotorFactory<SynchronizedMotor>();
            _port1 = new SerialPort(@"COM1");
            _port2 = new SerialPort(@"COM4");

            _handler1 = await factory.CreateFirstOrFromAddressAsync(_port1, 1);
            _handler2 = await factory.CreateFirstOrFromAddressAsync(_port2, 1);
        }

        [TearDown]
        public void TearDown()
        {
            _handler1?.Dispose();
            _handler2?.Dispose();
            _port1?.Dispose();
            _port2?.Dispose();
        }

        [Test]
        [TestCase(-1_000)]
        [TestCase(-8_000)]
        [TestCase(-16_000)]
        [TestCase(-32_000)]
        public async Task Test(int param)
        {
            await Task.WhenAll(_handler1.ReturnToOriginAsync(), _handler2.ReturnToOriginAsync());
            var positions = await Task.WhenAll(_handler1.GetActualPositionAsync(), _handler2.GetActualPositionAsync());
            CollectionAssert.AreEquivalent(new[] { 0, 0 }, positions);

            var responses = await Task.WhenAll(
                _handler1.SendCommandAsync(Command.MoveToPosition, param, CommandParam.MoveType.Absolute),
                _handler2.SendCommandAsync(Command.MoveToPosition, param, CommandParam.MoveType.Absolute));
            CollectionAssert.AreEquivalent(new[] {true, true}, responses.Select(x => x.Status == ReturnStatus.Success));

            await Task.WhenAll(_handler1.WaitForPositionReachedAsync(), _handler2.WaitForPositionReachedAsync());

            positions = await Task.WhenAll(_handler1.GetActualPositionAsync(), _handler2.GetActualPositionAsync());
            CollectionAssert.AreEquivalent(new[] { param, param }, positions);

            await Task.WhenAll(_handler1.ReturnToOriginAsync(), _handler2.ReturnToOriginAsync());
            positions = await Task.WhenAll(_handler1.GetActualPositionAsync(), _handler2.GetActualPositionAsync());
            CollectionAssert.AreEquivalent(new[] { 0, 0 }, positions);
        }

    }
}
