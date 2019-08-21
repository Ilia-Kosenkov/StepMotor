using System.IO;
using System.Threading.Tasks;

namespace RotationBenchmark
{
    internal static class Extensions
    {
        public static Task Log<T>(this StreamWriter @this, T actual, T @internal)
        {
            return @this.WriteLineAsync($"{actual,15}{@internal,15}");
        }
    }
}
