//    This file is part of Dipol-3 Camera Manager.

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
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StepMotor;

namespace DebugTests
{
    [TestFixture]
    public class StepMotorStaticTests
    {
        [Test]
        public async Task Test_SimpleCommand()
        {
            var ports = SerialPort.GetPortNames();

            //var factory = new StepMotorFactory();
            var factory = new StepMotorProvider<SynchronizedMotor>(NUnitLogger.Instance);

            var devices =
                await Task.WhenAll(ports.Select(x => new SerialPort(x)).Select(async x => (Port: x, Addresses: await factory.FindDeviceAsync(x))));

            Assert.IsTrue(devices.Any(x => x.Addresses.Count > 0));

           foreach(var (port, _) in devices)
                port.Dispose();
        }
    }

    [TestFixture]
    public class StepMotorTests
    {
        private IAsyncMotor _motor;
        private SerialPort _port;
        [SetUp]
        public async Task SetUp()
        {
            _port = new SerialPort("COM1");
            _motor = await new StepMotorProvider<SynchronizedMotor>(NUnitLogger.Instance).CreateFirstOrFromAddressAsync(_port, 1);
            await _motor.ReturnToOriginAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _motor?.Dispose();
            _port?.Dispose();
        }

        [Theory]
        public async Task Test_Rotation()
        {

            // ReSharper disable once RedundantArgumentDefaultValue
            var reply = await _motor.SendCommandAsync(Command.MoveToPosition, 10000, CommandParam.MoveType.Absolute);
            Assert.AreEqual(ReturnStatus.Success, reply.Status);

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            reply = await _motor.SendCommandAsync(Command.GetAxisParameter, 1, CommandParam.Default);
            Assert.AreEqual(ReturnStatus.Success, reply.Status);
            Assert.AreEqual(10000, reply.ReturnValue);

            await _motor.ReturnToOriginAsync(CancellationToken.None);


            reply = await _motor.SendCommandAsync(Command.GetAxisParameter, 1, CommandParam.Default);
            Assert.AreEqual(ReturnStatus.Success, reply.Status);
            Assert.AreEqual(0, reply.ReturnValue);

        }

        [Theory]
        public async Task Test_Status()
        {
            await _motor.WaitForPositionReachedAsync();

            var status = await _motor.GetStatusAsync();
            var axisStatus = await _motor.GetRotationStatusAsync();
            var equivalence = status.Join(axisStatus, x => x.Key, y => y.Key, (x, y) => x.Value == y.Value)
                .All(x => x);

            var isReached = await _motor.IsTargetPositionReachedAsync();

            Assert.IsTrue(equivalence);
            Assert.IsTrue(isReached);
        }

        [Theory]
        [TestCase(200)]
        [TestCase(3_200)]
        [TestCase(16_000)]
        [TestCase(32_000)]
        [TestCase(64_000)]
        public async Task Test_WaitForPositionReached(int pos)
        {

            // Rotates to zero
            await _motor.ReturnToOriginAsync(CancellationToken.None);
            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());


            // Rotates to target position
            var reply = await _motor.SendCommandAsync(Command.MoveToPosition, pos, CommandParam.MoveType.Absolute);
            Assume.That(reply.Status, Is.EqualTo(ReturnStatus.Success));
            Assert.That(async () => await _motor.WaitForPositionReachedAsync(CancellationToken.None), Throws.Nothing);

            //await _motor.WaitForPositionReachedAsync();

            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(pos, await _motor.GetPositionAsync());


            // Returns back to zero
            await _motor.ReturnToOriginAsync(CancellationToken.None);

            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());

        }

        [Test]
        public async Task Test_ReferenceReturn()
        {
            await _motor.ReturnToOriginAsync(CancellationToken.None);
            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());

            // Rotates to target position
            var reply = await _motor.SendCommandAsync(Command.MoveToPosition, 51400, CommandParam.MoveType.Absolute);
            Assert.That(reply.Status, Is.EqualTo(ReturnStatus.Success));
            await _motor.WaitForPositionReachedAsync();
            Assert.AreEqual(51400, await _motor.GetPositionAsync());
            await _motor.ReferenceReturnToOriginAsync();
            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());
        }

        [Theory]
        [TestCase(32_000)]
        [TestCase(64_000)]
        public async Task Test_WaitForPositionReached_TimeOut(int pos)
        {

            // Rotates to zero
            Assume.That(await _motor.GetPositionAsync(), Is.EqualTo(0));

            // Rotates to target position
            var reply = await _motor.SendCommandAsync(Command.MoveToPosition, pos, CommandParam.MoveType.Absolute);
            Assume.That(reply.Status, Is.EqualTo(ReturnStatus.Success));
            Assert.ThrowsAsync<TimeoutException>(() =>
                _motor.WaitForPositionReachedAsync(timeOut: TimeSpan.FromMilliseconds(350)));

            Assert.That(async () => await _motor.WaitForPositionReachedAsync(), Throws.Nothing);
            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(pos, await _motor.GetPositionAsync());

        }

        [Test]
        public async Task Test_IsInMotion()
        {
            var pos = 64_000;
            // Rotates to zero
            await _motor.ReturnToOriginAsync(CancellationToken.None);

            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());


            // Rotates to target position
            var reply = await _motor.SendCommandAsync(Command.MoveToPosition, pos, CommandParam.MoveType.Absolute);
            Assume.That(reply.Status, Is.EqualTo(ReturnStatus.Success));

            Assert.IsTrue(await _motor.IsInMotionAsync());
            await _motor.WaitForPositionReachedAsync(CancellationToken.None);
            Assert.IsFalse(await _motor.IsInMotionAsync());

            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(pos, await _motor.GetPositionAsync());


            // Returns back to zero
            await _motor.ReturnToOriginAsync(CancellationToken.None);

            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());

        }

        [Test]
        public async Task Test_Stop()
        {
            var pos = 196_000;
            // Rotates to zero
            await _motor.ReturnToOriginAsync(CancellationToken.None);

            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());

            // Rotates to target position
            var reply = await _motor.SendCommandAsync(Command.MoveToPosition, pos, CommandParam.MoveType.Absolute);
            Assume.That(reply.Status, Is.EqualTo(ReturnStatus.Success));

            Assert.IsTrue(await _motor.IsInMotionAsync());
            Assert.IsFalse(await _motor.IsTargetPositionReachedAsync());

            await Task.Delay(TimeSpan.FromMilliseconds(200));

            await _motor.StopAsync();

            await Task.Delay(TimeSpan.FromMilliseconds(200));

            Assert.IsFalse(await _motor.IsInMotionAsync());
            Assert.IsFalse(await _motor.IsTargetPositionReachedAsync());

            Assert.AreNotEqual(pos, await _motor.GetPositionAsync());

            // Returns back to zero
            await _motor.ReturnToOriginAsync(CancellationToken.None);

            Assert.IsTrue(await _motor.IsTargetPositionReachedAsync());
            Assert.AreEqual(0, await _motor.GetPositionAsync());

        }

        [Test]
        [TestCase(25)]
        public async Task StressTestMotor(int n)
        {
            const int step = 3200;
            var timeout = TimeSpan.FromMilliseconds(100);
            await _motor.ReturnToOriginAsync();

            for (var i = 0; i < n; i++)
            {
                await _motor.SendCommandAsync(Command.MoveToPosition, step, CommandParam.MoveType.Relative);
                await _motor.WaitForPositionReachedAsync();
                await Task.Delay(timeout);
            }

            Assert.AreEqual(step * n, await _motor.GetPositionAsync());

            await _motor.ReturnToOriginAsync();
        }

    }
}
