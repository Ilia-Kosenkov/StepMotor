using System;
using System.Threading.Tasks;

namespace RotationBenchmark
{
    internal interface IBenchmark: IDisposable
    {
        int N { get; }
        Task Benchmark(IProgress<bool> reporter);
    }
}