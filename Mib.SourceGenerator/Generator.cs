using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

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

        var moduleNames = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "SnmpSharpNet.Mib.Attributes.MibModulesAttribute",
                (node, ct) => true,
                (c, ct) => c.Attributes.SelectMany(ParseMibModulesAttribute)
            )
            .SelectMany((x, ct) => x)
            .Collect();

        var mibFiles = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".mib", StringComparison.OrdinalIgnoreCase))
            .Collect();

        var loadedModules = moduleNames.Combine(mibFiles)
            .Select((x, ct) => LoadMibModules(x.Left, x.Right, ct));

        var oidCarriers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "SnmpSharpNet.Mib.Attributes.MibOidsAttribute",
                (node, ct) => true,
                (c, ct) => c.Attributes.Select(x => ParseMibOidsAttribute(c.TargetSymbol, x))
            )
            .SelectMany((x, ct) => x)
            .Collect();

        context.RegisterSourceOutput(oidCarriers.Combine(loadedModules), (ctx, x) =>
        {
            var unresolvedCarriers = x.Left.ReportAll(ctx).ToArray();
            if (!x.Right.OrReport(ctx, out var loadedModules)) { return; }

            if (unresolvedCarriers.Length <= 0) { return; }
 
            var carriers = unresolvedCarriers
                .Select(carrier => {
                    return (carrier.decl, (MibModule)loadedModules[carrier.ModuleName]);
                });
            
            ctx.AddSource("SnmpMibData.Generated.cs",
                string.Join("\n", Templating.DataFile(carriers))
            );
        });
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

    private static Result<(PartialClass decl, string ModuleName)> ParseMibOidsAttribute(
        ISymbol targetSymbol,
        AttributeData attribute
    ) {
        if (attribute.ConstructorArguments.Length == 0)
        {
            return Diagnostic.Create(Diagnostics.ParseAttributesError, Location.None, "ParseMibOidsAttribute");
        }

        var arg = attribute.ConstructorArguments[0];
        var maybeModuleName = arg.Value as string;

        if (maybeModuleName is not string moduleName)
        {
            return Diagnostic.Create(Diagnostics.ParseAttributesError, Location.None, "ParseMibOidsAttribute");
        }

        return (PartialClass.FromSymbol(targetSymbol), moduleName);
    }

    static private Result<IReadOnlyDictionary<string, IMibThatExports>> LoadMibModules(
        ImmutableArray<string> moduleNames,
        ImmutableArray<AdditionalText> mibs,
        CancellationToken ct
    ) {
        var modules = new Dictionary<string, IMibThatExports>((IDictionary<string, IMibThatExports>)BuiltinMib.All);

        // Create mibsByName with deduplication (prefer source files over build output)
        var mibsByName = new Dictionary<string, (string Path, SourceText? Text)>();
        foreach (var mib in mibs)
        {
            var key = Path.GetFileNameWithoutExtension(mib.Path);
            if (!mibsByName.ContainsKey(key))
            {
                mibsByName[key] = (mib.Path, mib.GetText(ct));
            }
        }

        foreach (var moduleName in moduleNames.Distinct())
        {
            if (!mibsByName.TryGetValue(moduleName, out var mibInfo))
            {
                return Diagnostic.Create(Diagnostics.ParseError, Location.None, moduleName);
            }
            var (mibPath, mibContents) = mibInfo;

            try
            {
                var mibText = mibContents?.ToString() ?? string.Empty;
                var module = MibParser.ParseModule(mibText, mibPath, modules);
                
                // Only add if not already present (Parse may have added it via imports/includes)
                if (!modules.ContainsKey(module.Identifier))
                {
                    modules[module.Identifier] = module;
                }
            }
            catch (Exception e)
            {
                return Diagnostic.Create(Diagnostics.ParseAttributesError, Location.None, $"{mibPath}: <{e.Message}>");
            }
        }

        return modules;
    }
}
