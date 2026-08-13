using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace LLMEval.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        // InProcess avoids needing a secondary build of TFMs in CI smoke runs.
        var config = DefaultConfig.Instance
            .AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(1)
                .WithIterationCount(3)
                .WithInvocationCount(16)
                .WithUnrollFactor(1));

        // Allow callers to pass --filter etc.; fall back to short in-process job.
        if (args.Length == 0)
            args = ["--filter", "*"];

        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        return 0;
    }
}
