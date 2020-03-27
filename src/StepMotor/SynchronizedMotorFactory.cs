#nullable enable
using System;
using System.Collections.Immutable;
using System.IO.Ports;
using System.Threading.Tasks;

namespace StepMotor
{
    public class SynchronizedMotorFactory : IAsyncMotorFactory
    {
        public Task<ImmutableList<Address>> FindDeviceAsync(SerialPort port, Address? startAddress = null, Address? endAddress = null)
        {
            throw new NotImplementedException();
        }

        public async Task<IAsyncMotor?> TryCreateFromAddressAsync(
            SerialPort port, Address address, TimeSpan defaultTimeOut = default)
        {
            _ = port ?? throw new ArgumentNullException(nameof(port));

            var motor = new SynchronizedMotor(port, address, defaultTimeOut);
            try
            {
                if ((await motor.SendCommandAsync(Command.GetAxisParameter, 1, CommandParam.Default, address, 0,
                    defaultTimeOut)).IsSuccess)
                    return motor;

                if (await motor.TrySwitchToBinary(address))
                    return motor;
            }
            catch (Exception)
            {
                // ignored
            }

            await motor.DisposeAsync();
            return null;

        }

        public Task<IAsyncMotor?> TryCreateFirstAsync(SerialPort port, Address? startAddress = null, Address? endAddress = null,
            TimeSpan defaultTimeOut = default)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncMotor> CreateFirstOrFromAddressAsync(SerialPort port, Address address, Address? startAddress = null,
            Address? endAddress = null, TimeSpan defaultTimeOut = default)
        {
            throw new NotImplementedException();
        }
       
    }
}