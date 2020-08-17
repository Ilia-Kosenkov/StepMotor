#nullable enable

using System;

namespace StepMotor
{
    internal readonly struct MotorId
    {
        public string PortName { get; }
        public Address Address { get; }

        public MotorId(string portName, Address address)
            => (PortName, Address) = (portName ?? throw new ArgumentNullException(nameof(portName)), address);

        public override string ToString() => $"{PortName}+{Address.RawValue:00}";
    }
}
