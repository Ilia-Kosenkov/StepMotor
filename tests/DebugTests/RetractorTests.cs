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

#nullable enable

using System.IO.Ports;
using System.Threading.Tasks;
using NUnit.Framework;
using StepMotor;

namespace DebugTests
{
    [TestFixture]
    public class RetractorMotorStaticTests
    {
        private IAsyncMotor _device;
        private SerialPort _port;
        private const string TestPort = @"COM4";
        private IAsyncMotorFactory _factory;

        [SetUp]
        public void SetUp()
        {
            //_factory = new StepMotorFactory();
            _factory = new SynchronizedMotorFactory<StepMotorHandler>();
        }

        [TearDown]
        public void TearDown()
        {
            
            _device?.Dispose();
            _device = null;

            _port?.Dispose();
            _port = null;
        }

        [Theory]
        public async Task Test_TryCreateFromAddress()
        {
            var ports = SerialPort.GetPortNames();
            Assume.That(ports, Contains.Item(TestPort));
            Assume.That(_device, Is.Null);

            _port = new SerialPort(TestPort);
            _device = await _factory.TryCreateFromAddressAsync(_port, 1);
            Assume.That(_device, Is.Not.Null);
            Assert.That(await _device.GetActualPositionAsync(), Is.EqualTo(0));

        }

        [Theory]
        public async Task Test_TryCreateFirst()
        {
            var ports = SerialPort.GetPortNames();
            Assume.That(ports, Contains.Item(TestPort));
            Assume.That(_device, Is.Null);
            _port = new SerialPort(TestPort);

            _device = await _factory.TryCreateFirstAsync(_port);
            Assume.That(_device, Is.Not.Null);
            
            Assert.That(await _device.GetActualPositionAsync(), Is.EqualTo(0));

        }

        [Theory]
        public async Task Test_CreateFirstOrFromAddress()
        {
            var ports = SerialPort.GetPortNames();
            Assume.That(ports, Contains.Item(TestPort));
            Assume.That(_device, Is.Null);
            _port = new SerialPort(TestPort);

            _device = await _factory.CreateFirstOrFromAddressAsync(_port, 1);
            Assume.That(_device, Is.Not.Null);

            Assert.That(await _device.GetActualPositionAsync(), Is.EqualTo(0));

        }

    }

    [TestFixture]
    public class RetractorMotorTests
    {
        private SerialPort _port;
        private IAsyncMotor _motor;
        private const string PortName = @"COM4";

        [SetUp]
        public async Task SetUp()
        {
            //var factory = new StepMotorFactory();
            var factory = new SynchronizedMotorFactory<StepMotorHandler>();
            _port = new SerialPort(PortName);
            _motor = await factory.CreateFirstOrFromAddressAsync(_port, 1);
        }

        [TearDown]
        public void TearDown()
        {
            _motor?.Dispose();
            _port?.Dispose();
        }

        [Test]
        [TestCase(-1_000)]
        [TestCase(-10_000)]
        [TestCase(-100_000)]
        [TestCase(-450_000)]
        public async Task Test(int pos)
        {
            await _motor.ReturnToOriginAsync();
            Assert.That(await _motor.GetActualPositionAsync(), Is.EqualTo(0));

            var reply = await _motor.MoveToPosition(pos, CommandParam.MoveType.Absolute);
            Assert.AreEqual(ReturnStatus.Success, reply.Status);

            await _motor.WaitForPositionReachedAsync();
            Assert.That(await _motor.GetActualPositionAsync(), Is.EqualTo(pos));

            await _motor.ReturnToOriginAsync();
            Assert.That(await _motor.GetActualPositionAsync(), Is.EqualTo(0));
        }
    }
}
