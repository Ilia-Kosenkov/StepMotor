using System;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using NUnit.Framework;
using StepMotor;

namespace DebugTests
{
    [TestFixture]
    public class PositioningTests
    {
        private SerialPort _port;
        private IAsyncMotorFactory _factory;
        private IAsyncMotor _motor;

        [SetUp]
        public async Task SetUp()
        {
            _port = new SerialPort("COM1");
            _factory = new StepMotorFactory();
            _motor = await _factory.CreateFirstOrFromAddressAsync(_port, 1);
        }
        [TearDown]
        public void TearDown()
        {
            _motor.Dispose();
            _port.Dispose();
        }

        private async Task<(int Pos, int Internal)> FakeJob(int pos)
        {
            await _motor.MoveToPosition(pos, CommandParam.MoveType.Absolute);
            await _motor.WaitForPositionReachedAsync();

            await Task.Delay(TimeSpan.FromMilliseconds(150));

            var actualPos = await _motor.GetAxisParameterAsync(CommandParam.AxisParameter.ActualPosition);
            var internalPos = await _motor.GetAxisParameterAsync(CommandParam.AxisParameter.EncoderPosition);
            return (actualPos, internalPos);
        }

        [Test]
        public async Task Test_ZeroPosition()
        {
            
            using (var str = new StreamWriter(Path.Combine(TestContext.CurrentContext.TestDirectory, "log.dat")))
            {
                void Write(string s)
                {
                    Console.WriteLine(s);
                    str.WriteLine(s);
                }

                void Log((int Pos, int Internal) input, int arg, int grp = 0)
                {
                    Write($"{arg,10} {input.Pos,10} {input.Internal,10} {grp, 5}");
                }

                await _motor.ReferenceReturnToOriginAsync();
                var actualPos = await _motor.GetAxisParameterAsync(CommandParam.AxisParameter.ActualPosition);
                var internalPos = await _motor.GetAxisParameterAsync(CommandParam.AxisParameter.EncoderPosition);

                Write($"{"Param",10} {"Position",10} {"Internal",10} {"Grp",5}");
                Write(new string('-', 40));
                Log((actualPos, internalPos), 0);
                Write(new string('-', 40));
                for (var i = 0; i <= 64; i++)
                {
                    var j = i % 16;
                    var n = i / 16;
                    if (j == 0)
                        await _motor.ReferenceReturnToOriginAsync();

                    Log(await FakeJob(3200 * j), j, n);
                }
            }


            Assert.Pass();

        }
    }
}
