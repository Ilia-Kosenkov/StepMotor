#nullable enable
using System;
using System.Collections.Immutable;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

namespace StepMotor
{
    public class SynchronizedMotorFactory<T> : IAsyncMotorFactory where T : StepMotor
    {

        public async Task<ImmutableList<Address>> FindDeviceAsync(
            SerialPort port, Address? startAddress = null, Address? endAddress = null)
        {
            startAddress ??= Address.DefaultStart;
            endAddress ??= Address.DefaultEnd;

            var result = ImmutableList.CreateBuilder<Address>();

            for (var address = startAddress.Value.RawValue; address <= endAddress.Value.RawValue; address++)
            {
                var motor = CreateMotor(port, address, default);
                try
                {
                    if(await CheckMotorStatus(motor))
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

            var motor = CreateMotor(port, address, defaultTimeOut);
            if (await CheckMotorStatus(motor))
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

        private static async Task<bool> CheckMotorStatus(IAsyncMotor motor)
        {
            try
            {
                if ((await motor.SendCommandAsync(Command.GetAxisParameter, 1, CommandParam.Default, 0)).IsSuccess)
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
                if (await motor.TrySwitchToBinary())
                    return true;
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }


        private static IAsyncMotor CreateMotor(SerialPort port, Address? address, TimeSpan timeOut)
        {
            var args = new[] { typeof(SerialPort), typeof(Address?), typeof(TimeSpan) };
            var ctor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, args, null);
            if (ctor is null)
                throw new InvalidOperationException($"Type {typeof(T).Name} does not have specific constructor.");

            return ctor.Invoke(new object[] {port, address!, timeOut}) as IAsyncMotor
                   ?? throw new InvalidOperationException(
                       $"Type mismatch: {typeof(T).Name} does not implement {typeof(IAsyncMotor).Name}");
        }
    }
}