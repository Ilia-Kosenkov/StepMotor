using System;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using StepMotor;

namespace RotationBenchmark
{
    internal abstract class RotatorBase : IBenchmark
    {
        private readonly SerialPort _port;
        protected StreamWriter Writer;
        private readonly IAsyncMotorFactory _factory;
        protected int Param;
        protected TimeSpan Delay;
        protected IAsyncMotor Motor;

        protected RotatorBase(SerialPort port, 
            IAsyncMotorFactory factory, 
            string path,
            int param, int nRepeats, TimeSpan delay = default)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(nameof(path));

            path = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(path) ?? ".";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);


            _port = port;
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Param = param > 0 ? param : throw new ArgumentOutOfRangeException(nameof(param));
            N = nRepeats > 0 ? nRepeats : throw new ArgumentOutOfRangeException(nameof(nRepeats));

            Delay = delay == default ? TimeSpan.FromMilliseconds(100) : delay;

            var newPath = Path.Combine(dir, $"{name}_{param:000000}{ext}");
            Writer = new StreamWriter(new FileStream(newPath, FileMode.Create, FileAccess.Write));
        }

        public int N { get; }

        public abstract Task Benchmark(IProgress<bool> reporter);

        protected async Task InitMotor()
        {
            if (Motor is null)
                Motor = await _factory.TryCreateFromAddressAsync(_port, 1) 
                         ?? throw new InvalidOperationException(@"Failed to create stepper motor.");

            await Writer.Log("Actual", "Internal");
        }

        public void Dispose()
        {
            Writer.Dispose();
            Motor.Dispose();
        }
    }
}