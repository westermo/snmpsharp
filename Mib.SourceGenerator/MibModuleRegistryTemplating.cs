using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

/// <summary>
/// Emits <c>Snmp.GeneratedMibModules</c>, which embeds the source text of every compiled MIB so the
/// module ASTs can be parsed at runtime and combined with MIBs loaded at runtime.
/// </summary>
internal static class MibModuleRegistryTemplating
{
    public const string ClassName = "GeneratedMibModules";
    public const string HintName = "Snmp.GeneratedMibModules.generated.cs";

    private static readonly string[] RequiredRuntimeTypes =
    [
        "SnmpSharpNet.Mib.Ast.ModuleDefinition",
        "SnmpSharpNet.Mib.MibParser",
        "SnmpSharpNet.Mib.MibTree",
    ];

    /// <summary>The registry depends on SnmpSharpNet.Mib at runtime; only emit it when that assembly is referenced.</summary>
    public static bool CanEmit(Compilation compilation) =>
        RequiredRuntimeTypes.All(name => compilation.GetTypeByMetadataName(name) is not null);

    public static GeneratedSource Generate(IReadOnlyDictionary<string, string> sources)
    {
        var ordered = sources.OrderBy(source => source.Key, StringComparer.Ordinal).ToArray();
        List<string> lines =
        [
            "#nullable enable",
            "using System.CodeDom.Compiler;",
            "",
            "namespace Snmp",
            "{",
            "\t/// <summary>",
            "\t/// MIB modules compiled into this assembly by the SnmpSharpNet MIB source generator.",
            "\t/// The module ASTs are parsed lazily from the embedded MIB sources on first access.",
            "\t/// </summary>",
            $"\t{Templating.Attribute}",
            $"\tinternal static partial class {ClassName}",
            "\t{",
            "\t\tprivate static readonly global::System.Lazy<global::System.Collections.Generic.IReadOnlyDictionary<string, global::SnmpSharpNet.Mib.Ast.ModuleDefinition>> asts =",
            "\t\t\tnew(ParseAll, global::System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);",
            "",
            "\t\t/// <summary>Identifiers of the compiled MIB modules.</summary>",
            "\t\tpublic static global::System.Collections.Generic.IReadOnlyList<string> ModuleNames { get; } = new string[]",
            "\t\t{",
            .. ordered.Select(source => $"\t\t\t{Literal(source.Key)},"),
            "\t\t};",
            "",
            "\t\t/// <summary>Source text of each compiled MIB module, keyed by module identifier.</summary>",
            "\t\tpublic static global::System.Collections.Generic.IReadOnlyDictionary<string, string> Sources { get; } =",
            "\t\t\tnew global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.Ordinal)",
            "\t\t\t{",
            .. ordered.Select(source => $"\t\t\t\t[{Literal(source.Key)}] = {VerbatimLiteral(source.Value)},"),
            "\t\t\t};",
            "",
            "\t\t/// <summary>Parsed ASTs of the compiled MIB modules, keyed by module identifier.</summary>",
            "\t\tpublic static global::System.Collections.Generic.IReadOnlyDictionary<string, global::SnmpSharpNet.Mib.Ast.ModuleDefinition> Asts => asts.Value;",
            "",
            "\t\t/// <summary>",
            "\t\t/// Resolves the compiled modules together with <paramref name=\"additional\"/> modules (for example MIBs",
            "\t\t/// loaded at runtime). Additional modules with the same identifier replace compiled ones.",
            "\t\t/// </summary>",
            "\t\tpublic static global::System.Collections.Generic.IReadOnlyDictionary<string, global::SnmpSharpNet.Mib.MibModule> Resolve(",
            "\t\t\tglobal::System.Collections.Generic.IEnumerable<global::SnmpSharpNet.Mib.Ast.ModuleDefinition>? additional = null) =>",
            "\t\t\tglobal::SnmpSharpNet.Mib.MibParser.ParseModules(",
            "\t\t\t\tglobal::System.Linq.Enumerable.Concat(Asts.Values, additional ?? global::System.Array.Empty<global::SnmpSharpNet.Mib.Ast.ModuleDefinition>()));",
            "",
            "\t\t/// <summary>Builds the merged OID tree of the compiled modules and any <paramref name=\"additional\"/> modules.</summary>",
            "\t\tpublic static global::SnmpSharpNet.Mib.MibTreeNode BuildTree(",
            "\t\t\tglobal::System.Collections.Generic.IEnumerable<global::SnmpSharpNet.Mib.Ast.ModuleDefinition>? additional = null) =>",
            "\t\t\tglobal::SnmpSharpNet.Mib.MibTree.Build(Resolve(additional));",
            "",
            "\t\tprivate static global::System.Collections.Generic.IReadOnlyDictionary<string, global::SnmpSharpNet.Mib.Ast.ModuleDefinition> ParseAll()",
            "\t\t{",
            "\t\t\tvar result = new global::System.Collections.Generic.Dictionary<string, global::SnmpSharpNet.Mib.Ast.ModuleDefinition>(global::System.StringComparer.Ordinal);",
            "\t\t\tforeach (var source in Sources)",
            "\t\t\t{",
            "\t\t\t\tresult[source.Key] = global::SnmpSharpNet.Mib.MibParser.ParseAst(source.Value, source.Key);",
            "\t\t\t}",
            "",
            "\t\t\treturn result;",
            "\t\t}",
            "\t}",
            "}",
        ];

        return new GeneratedSource(HintName, string.Join("\n", lines));
    }

    private static string Literal(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string VerbatimLiteral(string value) =>
        "@\"" + value.Replace("\"", "\"\"") + "\"";
}
