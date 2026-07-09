using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SnmpSharpNet.Mib.SourceGenerator;

using PartialClass = (string Accessibility, string? Namespace, string TypeName);

[Generator]
public sealed class SnmpGenerator : IIncrementalGenerator
{

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(i => {
            i.AddEmbeddedAttributeDefinition();
            i.AddSource("SnmpAttributes.Generated.cs", Templating.Attributes);
        });

        var allMibs = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".mib", StringComparison.OrdinalIgnoreCase))
            .Select((text, ct) => {
                var moduleName = Path.GetFileNameWithoutExtension(text.Path);
                var module = ContextlessMibModule.TryFromAdditionalText(text, ct);
                return (moduleName, module);
            })
            .Collect();

        var mibMods = allMibs
            .Select((modules, ct) =>
            {

                var errors = modules
                    .Where(x => !x.module.IsOk)
                    .Select(x => x.module.Diagnostic)
                    .ToList();

                var parseds = modules
                    .Where(x => x.module.IsOk)
                    .ToDictionary(x => x.moduleName, x => x.module.Value);

                var cache = new Dictionary<string, IMibThatExports>((Dictionary<string, IMibThatExports>)BuiltinMib.All);
                var building = new HashSet<string>(StringComparer.Ordinal);

                void buildModule(string key)
                {
                    if (cache.ContainsKey(key)) { return; }

                    if (!building.Add(key))
                    {
                        errors.Add(Diagnostic.Create(
                            Diagnostics.ParseAttributesError,
                            Location.None,
                            $"{key}: <Recursive module dependency detected>"));
                        return;
                    }

                    if (!parseds.TryGetValue(key, out var parsed))
                    {
                        errors.Add(Diagnostic.Create(
                            Diagnostics.MibNotFound,
                            Location.None,
                            key));
                        building.Remove(key);
                        return;
                    }

                    foreach (var (symbols, fromModule) in parsed.Module.Imports)
                    {
                        buildModule(fromModule.ToString());
                    }

                    try
                    {
                        cache[key] = new MibModule(parsed.Module, cache);
                    }
                    catch (Exception e)
                    {
                        errors.Add(Diagnostic.Create(
                            Diagnostics.ParseAttributesError,
                            Location.None,
                            $"{key}: <{e.Message}>"));
                    }
                    finally
                    {
                        building.Remove(key);
                    }
                }

                // Todo: Valued dictionary
                var output = new Dictionary<string, MibModule>();
                foreach (var moduleName in modules.Select(x => x.moduleName).Distinct(StringComparer.Ordinal))
                {
                    buildModule(moduleName);
                    if (cache.TryGetValue(moduleName, out var mod) && mod is MibModule mibmod)
                    {
                        output[moduleName] = mibmod;
                    }
                }

                return (output, errors.ToImmutableArray());
            });


        var oidCarriers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "SnmpSharpNet.Mib.Attributes.MibOidsAttribute",
                (node, ct) => true,
                (c, ct) => (PartialClass.FromSymbol(c.TargetSymbol), c.Attributes.SelectMany(ParseMibModulesAttribute).ToImmutableArray())
            )
            .Collect();

        context.RegisterSourceOutput(mibMods.Combine(oidCarriers), (ctx, x) =>
        {
            foreach (var diag in x.Left.Item2)
            {
                ctx.ReportDiagnostic(diag);
            }

            var unresolvedCarriers = x.Right;
            var mibMods = x.Left.output;

            if (unresolvedCarriers.Length <= 0) { return; }
            var carriers = unresolvedCarriers
                .Select(carrier => GetRelevantItems(carrier.Item2, mibMods).Select(items => (carrier.Item1, items)))
                .ReportAll(ctx)
                .Select(carrier => {
                    return (carrier.Item1, carrier.items);
                });
            
            ctx.AddSource("SnmpMibData.Generated.cs",
                string.Join("\n", Templating.DataFile(carriers))
            );
        });
    }

    private static Result<ImmutableArray<MibItem>> GetRelevantItems(ImmutableArray<string> filters, Dictionary<string, MibModule> mibMods)
    {
        var items = new List<MibItem>();
        foreach (var filter in filters)
        {
            var split = filter.Split(["::"], StringSplitOptions.None);
            if (split.Length != 1 && split.Length != 2)
            {
                return Diagnostic.Create(
                    Diagnostics.ParseAttributesError,
                    Location.None,
                    $"Module filter '{filter}' is invalid."
                );
            }

            var moduleName = split[0];
            if (!mibMods.TryGetValue(moduleName, out var module))
            {
                return Diagnostic.Create(Diagnostics.MibNotFound, Location.None, moduleName);
            }

            if (split.Length == 1)
            {
                items.AddRange(module.Items.Values);
            }
            else
            {
                if (!module.TryImportObject(split[1], out var item))
                {
                    return Diagnostic.Create(Diagnostics.MibNotFound, Location.None, moduleName);
                }
                items.Add(item);
            }
        }

        return items.ToImmutableArray();
    }

    private static IEnumerable<string> ParseMibModulesAttribute(AttributeData x)
    {
            if (x.ConstructorArguments.Length == 0)
            {
                return [];
            }

            var arg = x.ConstructorArguments[0];

            if (arg.Kind == TypedConstantKind.Array)
            {
                return arg.Values
                    .Select(v => v.Value as string)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!);
            }

            if (arg.Value is string single && !string.IsNullOrWhiteSpace(single))
            {
                return [single];
            }

            return [];
    }
}
