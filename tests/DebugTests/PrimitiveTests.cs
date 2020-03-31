using System;
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
            IAsyncMotorFactory factory = new StepMotorProvider<SynchronizedMotor>();
            var port = new SerialPort("COM4");
            var motor = await factory.TryCreateFromAddressAsync(port, 1, TimeSpan.FromSeconds(2));
            Assert.That(motor, Is.Not.Null);

            await motor!.MoveToPosition(1000);
            await motor!.WaitForPositionReachedAsync();
            
            await motor.DisposeAsync();
            port.Dispose();
        }
    }
}
