using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SnmpSharpNet.Mib.SourceGenerator;

using AllMibs = (Dictionary<string, MibModule>?, ImmutableArray<Diagnostic>?);

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
            .Collect()
            .Select<ImmutableArray<AdditionalText>, AllMibs>((texts, ct) =>
            {
                List<Diagnostic> errors = [];

                var asts = texts
                    .Select(text => GenMibDefinition.FromAdditionalText(text, ct))
                    .Where(x => !x.CollectError(errors))
                    .ToDictionary(x => x.Name, x => x);

                var modules = asts.ConstructModules();
                if (!modules.IsOk)
                {
                    errors.Add(modules.Diagnostic);
                }
            
                return errors.Count > 0
                    ? (null, errors.ToImmutableArray())
                    : (new Dictionary<string, MibModule>(modules.Value), null);
            });

        var oidCarriers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "SnmpSharpNet.Mib.Attributes.MibOidsAttribute",
                (node, ct) => true,
                (c, ct) => (PartialClass.FromSymbol(c.TargetSymbol), c.Attributes.SelectMany(ParseMibOidsAttribute).ToImmutableArray())
            )
            .Collect();

        context.RegisterSourceOutput(allMibs.Combine(oidCarriers), (ctx, x) =>
        {
            if (x.Left.Item2 is {} errors)
            {
                foreach (var diag in errors)
                {
                    ctx.ReportDiagnostic(diag);
                }
                return;
            }

            var unresolvedCarriers = x.Right;
            var mibMods = x.Left.Item1!;

            if (unresolvedCarriers.Length <= 0) { return; }
            var carriers = unresolvedCarriers
                .Select(carrier => GetRelevantItems(carrier.Item1.Location, carrier.Item2, mibMods).Select(items => (carrier.Item1, items)))
                .ReportAll(ctx)
                .Select(carrier => {
                    return (carrier.Item1, carrier.items);
                });
            
            ctx.AddSource("SnmpMibData.Generated.cs",
                string.Join("\n", Templating.DataFile(carriers))
            );
        });
    }

    private static Result<ImmutableArray<MibItem>> GetRelevantItems(Location location, ImmutableArray<string> filters, Dictionary<string, MibModule> mibMods)
    {
        var items = new List<MibItem>();
        foreach (var filter in filters)
        {
            var split = filter.Split(["::"], StringSplitOptions.None);
            if (split.Length != 1 && split.Length != 2)
            {
                return Diagnostic.Create(
                    Diagnostics.ParseAttributesError,
                    location,
                    $"Module filter '{filter}' is invalid."
                );
            }

            var moduleName = split[0];
            if (!mibMods.TryGetValue(moduleName, out var module))
            {
                return Diagnostic.Create(Diagnostics.MibNotFound, location, $"MibOids: Unknown module '{moduleName}'");
            }

            if (split.Length == 1)
            {
                items.AddRange(module.Items.Values);
            }
            else
            {
                if (!module.TryImportObject(split[1], out var item))
                {
                    return Diagnostic.Create(Diagnostics.MibNotFound, location, $"MibOids: Unknown item '{split[1]}' in '{moduleName}'");
                }
                items.Add(item);
            }
        }

        return items.ToImmutableArray();
    }

    private static IEnumerable<string> ParseMibOidsAttribute(AttributeData x)
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
