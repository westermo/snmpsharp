using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SnmpSharpNet.Mib.SourceGenerator;

using PartialClass = (string Accessibility, string Namespace, string TypeName);

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
                    #pragma warning disable CS9113
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
                    #pragma warning restore CS9113
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
            var (loadedModules, loadError) = x.Right;
            if (loadedModules is null)
            {
                ctx.ReportDiagnostic(loadError!);
                return;
            }

            if (x.Left.Length <= 0) { return; }
 
            var carriers = x.Left.Select(
                x => (x.decl, (MibModule)loadedModules[x.ModuleName])
            );
            
            ctx.AddSource("SnmpMibData.Generated.cs",
                string.Join("\n", TemplateDataFile(carriers))
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

    private static IEnumerable<(PartialClass decl, string ModuleName)> ParseMibOidsAttribute(
        ISymbol targetSymbol,
        AttributeData attribute)
    {
        // TODO: Error handling
        if (attribute.ConstructorArguments.Length == 0) { return []; }

        var arg = attribute.ConstructorArguments[0];
        var maybeModuleName = arg.Value as string;

        if (maybeModuleName is not {} moduleName) { return []; }

        return [
            (PartialClass.FromSymbol(targetSymbol), moduleName)
        ];
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
                if (!modules.ContainsKey(module.Identifier))
                {
                    modules[module.Identifier] = module;
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

    static private string[] TemplateDataFile(IEnumerable<(PartialClass, MibModule)> decls)
    {
        return [
            $"using System.Collections.Generic;",
            $"using SnmpSharpNet;",
            $"",
            ..decls.SelectMany(x => TemplatePartialClass(x.Item1, x.Item2))
        ];
    }

    static private string[] TemplatePartialClass(PartialClass decl, MibModule module)
    {
        return decl.AsLiteral([
            ..module.Items.Values.SelectMany(TemplateOidField),
            $"",
            $"public readonly Oid[] AllOids = [",
            ..module.Items.Values.Select(
                item => $"\t{item.Ident.Name}Oid,"
            ),
            $"];",
            $"",
            ..TemplateFromValues(decl.TypeName, module.Items.Values)
        ]);
    }

    static private string[] TemplateOidField(MibItem item)
    {
        return [
            $"",
            $"/// <summary>",
            $"/// From SNMP Mib",
            $"/// {item.Ident}",
            $"/// </summary>",
            $"public static readonly Oid {item.Ident.Name}Oid = {item.Ident.AsLiteral()};",
            $"",
            $"/// <summary>",
            $"/// From SNMP Mib",
            $"/// {item.Ident}",
            $"/// </summary>",
            $"public required AsnType {item.Ident.Name};"
        ];
    }


    static private string[] TemplateFromValues(string type, IEnumerable<MibItem> items)
    {
        return [
            $"public static {type}? FromValues(IReadOnlyDictionary<Oid, AsnType> values)",
            $"{{",
            ..items.Select(
                x => $"\tif(!values.TryGetValue({x.Ident.Name}Oid, out var _{x.Ident.Name})) {{ return null; }}"
            ),
            $"",
            $"\treturn new {type}()",
            $"\t{{",
            ..items.Select(x => $"\t\t{x.Ident.Name} = _{x.Ident.Name},"),
            $"\t}};",
            $"}}",
        ];
    }
}