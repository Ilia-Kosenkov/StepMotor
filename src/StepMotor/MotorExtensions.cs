#nullable enable
using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace StepMotor
{
    public static class MotorExtensions
    {
        public static async Task<IAsyncMotor?> TryCreateFirstAsync(
            this IAsyncMotorFactory factory,
            SerialPort port,
            Address? startAddress = null,
            Address? endAddress = null,
            TimeSpan defaultTimeOut = default)
        {
            _ = factory ?? throw new ArgumentNullException(nameof(factory));
            _ = port ?? throw new ArgumentNullException(nameof(port));
            startAddress ??= Address.DefaultStart;
            endAddress ??= Address.DefaultEnd;

            if (startAddress > endAddress)
                throw new ArgumentOutOfRangeException(
                    $"[{nameof(startAddress)}] should be less than or equal to [{nameof(endAddress)}]");

            for (var address = startAddress.Value.RawValue; address <= endAddress.Value.RawValue; address++)
                if (await factory.TryCreateFromAddressAsync(port, address, defaultTimeOut) is { } motor)
                    return motor;

            return null;
        }

        // ReSharper disable once UnusedMember.Global
        public static async Task<IAsyncMotor> CreateFirstOrFromAddressAsync(
            this IAsyncMotorFactory factory,
            SerialPort port,
            Address address,
            Address? startAddress = null,
            Address? endAddress = null,
            TimeSpan defaultTimeOut = default)
            => factory switch
            {
                null => throw new ArgumentNullException(nameof(factory)),
                not null =>
                    (await factory.TryCreateFromAddressAsync(port, address, defaultTimeOut)
                        ?? await factory.TryCreateFirstAsync(port, startAddress, endAddress, defaultTimeOut))
                        ?? throw new InvalidOperationException("Failed to connect to step motor.")
            };

    }
}
