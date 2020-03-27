#nullable enable
using System;
using System.Collections.Immutable;
using System.IO.Ports;
using System.Threading.Tasks;

namespace StepMotor
{
    public class SynchronizedMotorFactory : IAsyncMotorFactory
    {
        public async Task<ImmutableList<Address>> FindDeviceAsync(
            SerialPort port, Address? startAddress = null, Address? endAddress = null)
        {
            startAddress ??= Address.DefaultStart;
            endAddress ??= Address.DefaultEnd;

            var result = ImmutableList.CreateBuilder<Address>();

            for (var address = startAddress.Value.RawValue; address <= endAddress.Value.RawValue; address++)
            {
                var motor = new SynchronizedMotor(port, address);
                try
                {
                    if(await CheckMotorStatus(motor, address, default))
                        result.Add(address);
                }
                catch (Exception)
                {
                    // Ignored
                }
                finally
                {
                    motor.Dispose();
                }
            }

            return result.ToImmutable();
        }

        public async Task<IAsyncMotor?> TryCreateFromAddressAsync(
            SerialPort port, Address address, TimeSpan defaultTimeOut = default)
        {
            _ = port ?? throw new ArgumentNullException(nameof(port));

            var motor = new SynchronizedMotor(port, address, defaultTimeOut);
            if (await CheckMotorStatus(motor, address, defaultTimeOut))
                return motor;

            await motor.DisposeAsync();
            return null;
        }

        public async Task<IAsyncMotor?> TryCreateFirstAsync(SerialPort port, Address? startAddress = null, Address? endAddress = null,
            TimeSpan defaultTimeOut = default)
        {
            _ = port ?? throw new ArgumentNullException(nameof(port));
            startAddress ??= Address.DefaultStart;
            endAddress ??= Address.DefaultEnd;

            if (startAddress > endAddress)
                throw new ArgumentOutOfRangeException(
                    $"[{nameof(startAddress)}] should be less than or equal to [{nameof(endAddress)}]");

            for (var address = startAddress.Value.RawValue; address <= endAddress.Value.RawValue; address++)
                if (await TryCreateFromAddressAsync(port, address, defaultTimeOut) is { } motor)
                    return motor;

            return null;
        }

        public async Task<IAsyncMotor> CreateFirstOrFromAddressAsync(SerialPort port, Address address, Address? startAddress = null,
            Address? endAddress = null, TimeSpan defaultTimeOut = default)
            => (await TryCreateFromAddressAsync(port, address, defaultTimeOut)
                ?? await TryCreateFirstAsync(port, startAddress, endAddress, defaultTimeOut))
               ?? throw new InvalidOperationException("Failed to connect to step motor.");

        private static async Task<bool> CheckMotorStatus(SynchronizedMotor motor, Address address, TimeSpan timeOut)
        {
            try
            {
                if ((await motor.SendCommandAsync(Command.GetAxisParameter, 1, CommandParam.Default, address, 0,
                    timeOut)).IsSuccess)
                    return true;
            }
            catch (TimeoutException)
            {
                // Ignored
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                if (await motor.TrySwitchToBinary(address))
                    return true;
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

    }
}