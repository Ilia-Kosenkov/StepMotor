using System;
using System.Threading.Tasks;

namespace RotationBenchmark
{
    internal interface IBenchmark: IDisposable
    {
        Task Benchmark(IProgress<bool> reporter);
    }
}