// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using SnmpSharpNet.Benchmarks.Security;

BenchmarkSwitcher.FromAssembly(typeof(AuthenticationBenchmarks).Assembly)
    .Run(args.Length == 0 ? ["--filter", $"*{nameof(AuthenticationBenchmarks)}.*"] : args);