// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using SnmpSharpNet.Benchmarks.Security;

var summary = BenchmarkRunner.Run<AuthenticationBenchmarks>();