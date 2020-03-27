using System.IO.Ports;
using System.Threading.Tasks;
using NUnit.Framework;
using StepMotor;

#nullable enable
namespace DebugTests
{
    [TestFixture]
    public class PrimitiveTests
    {
        [Test]
        public async Task Test_Initialization()
        {
            IAsyncMotorFactory factory = new SynchronizedMotorFactory();
            var port = new SerialPort("COM1");
            var motor = await factory.TryCreateFromAddressAsync(port, 1);

        }
    }
}
