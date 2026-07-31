using BenchmarkDotNet.Running;
using SnmpSharpNet.Mib.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(MibParsingBenchmarks).Assembly).Run(args);
