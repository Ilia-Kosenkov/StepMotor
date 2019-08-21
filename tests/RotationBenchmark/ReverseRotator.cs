using System;
using System.IO.Ports;
using System.Threading.Tasks;
using StepMotor;

namespace RotationBenchmark
{
    internal sealed class ReverseRotator : RotatorBase
    {
        public ReverseRotator(SerialPort port, 
            IAsyncMotorFactory factory, 
            string path,
            int param, int nRepeats, TimeSpan delay = default) : base(port, factory, path, param, nRepeats, delay)
        {
        }

        public override async Task Benchmark(IProgress<bool> reporter)
        {
            await InitMotor();
            await Motor.ReferenceReturnToOriginAsync();
            await Motor.MoveToPosition(N * Param);
            await Motor.WaitForPositionReachedAsync();

            for (var i = 0; i < N; i++)
            {
                await Motor.MoveToPosition((N - i) * Param);
                await Motor.WaitForPositionReachedAsync();

                var delayTask = Task.Delay(Delay);
                reporter?.Report(true);
                await delayTask;

                var actual = await Motor.GetAxisParameterAsync(CommandParam.AxisParameter.ActualPosition);
                var @internal = await Motor.GetAxisParameterAsync(CommandParam.AxisParameter.EncoderPosition);

                await Writer.Log(actual, @internal);
            }
        }
    }
}
