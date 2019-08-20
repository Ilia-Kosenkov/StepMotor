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

        [Test]
        public async Task Test_ZeroPosition()
        {
            await _motor.ReturnToOriginAsync();
            var actualPos = await _motor.GetAxisParameterAsync(CommandParam.AxisParameter.ActualPosition);
            var internalPos = await _motor.GetAxisParameterAsync(CommandParam.AxisParameter.EncoderPosition);

            Assert.Fail();
        }
    }
}
