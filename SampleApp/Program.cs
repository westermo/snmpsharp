// See https://aka.ms/new-console-template for more information

using System.Net;
using Microsoft.Extensions.Logging;
using SampleApp;
using SnmpSharpNet;

var factory = LoggerFactory.Create(conf => conf.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = factory.CreateLogger<HighLevelSnmpClient>();
using var target = new UdpTarget(IPAddress.Parse(args[0]), 161, 2000, 1);
var parameters = new AgentParameters(SnmpVersion.Ver2, new OctetString("public"));
var client = new HighLevelSnmpClient(target, parameters, logger);
var result = client.WalkTree(new Oid("1"));
foreach (var pair in result)
{
    Console.WriteLine($"{pair.Key} => {pair.Value}");
}