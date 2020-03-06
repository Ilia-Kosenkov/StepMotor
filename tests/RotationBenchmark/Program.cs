using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StepMotor;

namespace RotationBenchmark
{
    internal class Program
    {
        private static async Task<int> Main()
        {

            await Debug();
            return 0;
        }

        private static async Task Debug()
        {
            using var port = new SerialPort(@"COM4");
            using var motor = await new StepMotorFactory().CreateFirstOrFromAddressAsync(port, 1);

            await motor.MoveToPosition(3200);
            await motor.WaitForPositionReachedAsync();
            var status = await motor.GetAxisParameterAsync(CommandParam.AxisParameter.ActualPosition);
            Console.WriteLine(status);
            await motor.ReturnToOriginAsync();
            status = await motor.GetAxisParameterAsync(CommandParam.AxisParameter.ActualPosition);
            Console.WriteLine(status);

        }

        private static async Task Job()
        {
            var pars = new[] { 100, 1_000, 1_600, 3_200, 4_800, 6_000 };
            var nRepeats = new[] { 60, 50, 40, 32, 32, 24 };


            var total = nRepeats.Sum() * 2;
            var current = 0;

            var factory = new StepMotorFactory();
            var reporter = new Progress<bool>();
            reporter.ProgressChanged += (sender, _) =>
            {
                Console.WriteLine($"{Interlocked.Increment(ref current),4}/{total,4}");
            };
            using (var port = new SerialPort(@"COM1"))
            {
                IEnumerable<IBenchmark> benchmarks = pars.Zip(nRepeats, (x, y) => new { Par = x, N = y })
                    .Select(x => new Rotator(port, factory, "log.dat", x.Par, x.N, TimeSpan.FromMilliseconds(150)));
                IEnumerable<IBenchmark> reverseBenchmarks = pars.Zip(nRepeats, (x, y) => new { Par = x, N = y })
                    .Select(x => new ReverseRotator(port, factory, "log_reverse.dat", x.Par, x.N, TimeSpan.FromMilliseconds(150)));

                foreach (var bench in benchmarks.Concat(reverseBenchmarks))
                {
                    Console.WriteLine(@"-----------------------------");
                    await bench.Benchmark(reporter);
                    bench.Dispose();
                }
            }

            if (Debugger.IsAttached)
                Console.ReadKey();
        }
    }
}
