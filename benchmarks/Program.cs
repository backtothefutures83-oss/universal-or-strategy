using BenchmarkDotNet.Running;

namespace V12_Performance.Benchmarks
{
    /// <summary>
    /// BenchmarkDotNet entry point for V12 performance harnesses.
    /// Run with: dotnet run -c Release
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // BenchmarkRunner will discover and run all benchmark classes
            // Placeholder until T05-T07 benchmarks are implemented
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}

// Made with Bob
