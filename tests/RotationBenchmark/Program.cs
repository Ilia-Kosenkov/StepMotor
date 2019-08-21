using System;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using StepMotor;

namespace RotationBenchmark
{
    internal class Program
    {
        private static async Task<int> Main()
        {

            var pars = new[] {100, 1_000, 1_600, 3_200, 4_800, 6_000};
            var nRepeats = new[] {20, 20, 20, 16, 16, 8};
            var factory = new StepMotorFactory();
            var reporter = new Progress<(int Current, int Total)>();
            reporter.ProgressChanged += (sender, tuple) => Console.WriteLine($"{tuple.Current,4}/{tuple.Total,4}");
            using (var port = new SerialPort(@"COM1"))
            {
                var benchmarks = pars.Zip(nRepeats, (x, y) => new {Par = x, N = y})
                    .Select(x => new Rotator(port, factory, "log.dat", x.Par, x.N, TimeSpan.FromMilliseconds(75)))
                    .ToList<IBenchmark>();
                foreach (var bench in benchmarks)
                    await bench.Benchmark(reporter);
            }

            return 0;
        }
    }
}
