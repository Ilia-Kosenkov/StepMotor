#nullable enable

namespace StepMotor
{
    public sealed class RefSearchParam : CommandParam
    {
        public RefSearchType Value { get; }

        public RefSearchParam(RefSearchType type) => Value = type;


        protected override byte AsByte() => (byte) Value;

        public static implicit operator RefSearchParam(RefSearchType type) => new RefSearchParam(type);

    }
}