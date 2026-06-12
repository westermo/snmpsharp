using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SnmpSharpNet.Mib;

namespace WeConfig.Language.SourceGenerators;

[Generator]
public sealed class SnmpGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor DiagParseError = new(
        id: "SNMP001",
        title: "Failed to parse SNMP MIB module",
        messageFormat: "Parsing failed when parsing '{0}': {1}",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor DiagModuleFindingError = new(
        id: "SNMP002",
        title: "Failed to find SNMP MIB module",
        messageFormat: "No <AdditionalFiles> file matches '**/*/{0}.mib'",
        category: "Design", DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context
            .RegisterPostInitializationOutput(i =>
            {
                i.AddEmbeddedAttributeDefinition();
                i.AddSource("SnmpAttributes.Generated.cs",
                    """
                    using System;

                    namespace SnmpSharpNet.Mib.Attributes
                    {
                        [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                        [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
                        public sealed class MibModulesAttribute(params string[] modules) : Attribute {}

                        [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                        [AttributeUsage(AttributeTargets.Class)]
                        public sealed class MibOidsAttribute(string module) : Attribute {}
                    }
                    """
                );
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
                (c, ct) => c.Attributes.SelectMany(x => ParseMibOidsAttribute(c.TargetSymbol, x))
            )
            .SelectMany((x, ct) => x)
            .Collect();

        context.RegisterSourceOutput(oidCarriers.Combine(loadedModules), (ctx, x) =>
        {
            var carriers = x.Left;
            var (loadedModules, loadError) = x.Right;
            if (loadedModules is null)
            {
                ctx.ReportDiagnostic(loadError!);
                return;
            }

            var source = string.Join("\n",
                carriers
                    .Select(carrier => {
                        try {
                            return TemplatePartialClass(carrier.Namespace, carrier.TypeName, (MibModule)loadedModules[carrier.ModuleName]);
                        } catch (Exception ex) {
                            return $"// ERROR generating {carrier.TypeName}: {ex.Message}";
                        }
                    })
            );

            if (carriers.Length > 0) {
                ctx.AddSource("SnmpMibData.Generated.cs", source);
            }
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

    private static IEnumerable<(string ModuleName, string Namespace, string TypeName)> ParseMibOidsAttribute(
        ISymbol targetSymbol,
        AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
        {
            return [];
        }

        var arg = attribute.ConstructorArguments[0];
        var moduleName = arg.Value as string;

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return [];
        }

        return [(moduleName!, targetSymbol.ContainingNamespace.ToString(), targetSymbol.Name)];
    }

    static private (IReadOnlyDictionary<string, IMibThatExports>?, Diagnostic?) LoadMibModules(
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
                return (null, Diagnostic.Create(DiagModuleFindingError, Location.None, moduleName));
            }
            var (mibPath, mibContents) = mibInfo;

            try
            {
                var mibText = mibContents?.ToString() ?? string.Empty;
                var module = MibModule.Parse(mibText, modules);
                
                // Only add if not already present (Parse may have added it via imports/includes)
                if (!modules.ContainsKey(module.identifier))
                {
                    modules[module.identifier] = module;
                }
            }
            catch (Exception ex)
            {
                return (null, Diagnostic.Create(
                    DiagParseError,
                    Location.None,
                    mibPath, ex.Message
                ));
            }
        }

        return (modules, null);
    }

    
    static private string TemplatePartialClass(string ns, string name, MibModule module)
    {
        return string.Join("\n", [
            $"namespace {ns}",
            $"{{",
            $"partial class {name}",
            $"{{",
            ..module.items.SelectMany(x => TemplateOidField(module.identifier, x.Value.name, x.Key)),
            $"}}",
            $"}}"
        ]);
    }

    static private string[] TemplateOidField(string modName, string fieldName, uint[] oid)
    {
        return [
            $"/// <summary>",
            $"/// From SNMP Mib",
            $"/// \\<{modName}\\>.{fieldName} = {string.Join(".", oid)}",
            $"/// </summary>",
            $"public static readonly Oid {fieldName} = new Oid([{string.Join(", ", oid)}]);"
        ];
    }
}