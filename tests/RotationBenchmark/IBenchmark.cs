using System;
using System.Threading.Tasks;

namespace RotationBenchmark
{
    internal interface IBenchmark: IDisposable
    {
        Task Benchmark(IProgress<(int Current, int Total)> reporter);
    }
}