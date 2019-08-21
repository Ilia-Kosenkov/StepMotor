using System;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using StepMotor;

namespace RotationBenchmark
{
    internal sealed class Rotator : IBenchmark
    {
        private readonly SerialPort _port;
        private readonly StreamWriter _writer;
        private readonly IAsyncMotorFactory _factory;
        private readonly int _param;
        private readonly int _nRepeats;
        private readonly TimeSpan _delay;

        private IAsyncMotor _motor;

        public Rotator(SerialPort port, 
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
            _param = param > 0 ? param : throw new ArgumentOutOfRangeException(nameof(param));
            _nRepeats = nRepeats > 0 ? nRepeats : throw new ArgumentOutOfRangeException(nameof(nRepeats));

            _delay = delay == default ? TimeSpan.FromMilliseconds(100) : delay;

            var newPath = Path.Combine(dir, $"{name}_{param:000000}{ext}");
            _writer = new StreamWriter(new FileStream(newPath, FileMode.OpenOrCreate, FileAccess.Write));
        }

        private async Task InitMotor()
        {
            if (_motor is null)
                _motor = await _factory.TryCreateFromAddressAsync(_port, 1) 
                         ?? throw new InvalidOperationException(@"Failed to create stepper motor.");

            await _writer.Log("Actual", "Internal");
        }

        public async Task Benchmark(IProgress<(int Current, int Total)> reporter)
        {
            await InitMotor();
            await _motor.ReferenceReturnToOriginAsync();
            for (var i = 0; i < _nRepeats; i++)
            {
                await _motor.MoveToPosition(i * _param);
                await _motor.WaitForPositionReachedAsync();

                var delayTask = Task.Delay(_delay);
                reporter?.Report((i, _nRepeats));
                await delayTask;

                var actual = _motor.GetAxisParameterAsync(CommandParam.AxisParameter.ActualPosition);
                var @internal = _motor.GetAxisParameterAsync(CommandParam.AxisParameter.EncoderPosition);

                await _writer.Log(actual, @internal);
            }
        }

      

        public void Dispose()
        {
            _writer.Dispose();
        }
    }
}
