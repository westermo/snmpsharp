using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

using AllMibs = (ValuedDictionary<string, MibModule>?, ImmutableArray<Diagnostic>?, ValuedDictionary<string, string>?, EquatableArray<string>?);

[Generator]
public sealed class SnmpGenerator : IIncrementalGenerator
{
    /// <summary>MSBuild property that controls embedding of MIB sources (and runtime AST access).</summary>
    public const string EmbedMibSourcesProperty = "SnmpSharpNetEmbedMibSources";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var embedSources = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => IsEnabled(
                options.GlobalOptions.TryGetValue($"build_property.{EmbedMibSourcesProperty}", out var value)
                    ? value
                    : null));

        var allMibs = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".mib", StringComparison.OrdinalIgnoreCase))
            .Collect()
            .Select<ImmutableArray<AdditionalText>, AllMibs>((texts, ct) =>
            {
                List<Diagnostic> errors = [];

                var defs = texts
                    .Select(text => GenMibDefinition.FromAdditionalText(text, ct))
                    .ToList();

                // Collect warnings from filename-mismatch cases before filtering errors
                var warnings = defs
                    .Where(d => d.Warnings is not null)
                    .SelectMany(d => d.Warnings!);

                var asts = defs
                    .Where(x => !x.CollectError(errors))
                    .ToDictionary(x => x.Name, x => x);

                var definitionDiagnostics = MibSemanticValidator.ValidateDefinitions(asts).ToArray();
                errors.AddRange(definitionDiagnostics.Where(diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error));
                warnings = warnings.Concat(definitionDiagnostics.Where(diagnostic =>
                    diagnostic.Severity != DiagnosticSeverity.Error));

                ValuedDictionary<string, MibModule>? resolvedModules = null;
                if (errors.Count == 0)
                {
                    var modules = asts.ConstructModules();
                    if (!modules.IsOk)
                    {
                        errors.Add(modules.Diagnostic);
                    }
                    else
                    {
                        resolvedModules = new ValuedDictionary<string, MibModule>(modules.Value);
                        var semanticDiagnostics = MibSemanticValidator.Validate(asts, resolvedModules).ToArray();
                        errors.AddRange(semanticDiagnostics.Where(diagnostic =>
                            diagnostic.Severity == DiagnosticSeverity.Error));
                        warnings = warnings.Concat(semanticDiagnostics.Where(diagnostic =>
                            diagnostic.Severity != DiagnosticSeverity.Error));
                    }
                }

                if (errors.Count > 0)
                {
                    return (null, [.. warnings, .. errors], null, null);
                }

                var sources = new ValuedDictionary<string, string>(
                    asts.ToDictionary(x => x.Key, x => x.Value.Source));

                // Builtin modules that are imported but not supplied as files are emitted from BuiltinMib.
                // Their own dependencies are included transitively so their parent arcs are always named.
                var builtins = MibTree.GetBuiltinDependencies(
                    asts.ToDictionary(x => x.Key, x => x.Value.Module.Value));

                // Return warnings together with the successful result so they can be emitted
                var allDiagnostics = warnings.ToImmutableArray();
                return (resolvedModules!,
                    allDiagnostics.Length > 0 ? allDiagnostics : null,
                    sources,
                    new EquatableArray<string>([.. builtins]));
            });

        var compilationTypeNames = allMibs
            .Select((mibs, _) =>
            {
                var names = MibModuleRegistryTemplating.RequiredRuntimeTypes.AsEnumerable();
                if (mibs.Item1 is not null)
                {
                    var tree = MibTree.Build(mibs.Item1, mibs.Item4);
                    names = names.Concat(TreeTemplating.GetCompilationTypeNames(
                        tree,
                        mibs.Item1.Select(module => module.Value)));
                }

                return new EquatableArray<string>([.. names.Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)]);
            })
            .WithTrackingName("MibCompilationTypeNames");

        // Only type-presence decisions affect generation, not unrelated compilation edits.
        var existingTypeNames = compilationTypeNames
            .Combine(context.CompilationProvider)
            .Select((pair, _) => new EquatableArray<string>([.. pair.Left
                .Where(name => pair.Right.GetTypeByMetadataName(name) is not null)
                .OrderBy(name => name, StringComparer.Ordinal)]))
            .WithComparer(EqualityComparer<EquatableArray<string>>.Default)
            .WithTrackingName("CompilationTypeAvailability");

        var generationInput = allMibs.Combine(existingTypeNames).Combine(embedSources)
            .WithTrackingName("MibGenerationInput");
        context.RegisterSourceOutput(generationInput, (ctx, x) =>
        {
            var mibs = x.Left.Left;
            var existingTypes = new HashSet<string>(x.Left.Right, StringComparer.Ordinal);
            var embed = x.Right;

            if (mibs.Item2 is {} diagnostics)
            {
                foreach (var diag in diagnostics)
                {
                    ctx.ReportDiagnostic(diag);
                }
                // Abort on errors (warnings still reported above, module is null for errors)
                if (mibs.Item1 is null) { return; }
            }

            var tree = MibTree.Build(mibs.Item1!, mibs.Item4);
            foreach (var source in TreeTemplating.Generate(
                         tree,
                         mibs.Item1!.Select(module => module.Value),
                         existingTypes))
            {
                ctx.AddSource(source.HintName, source.Source);
            }

            if (embed && mibs.Item3 is { Count: > 0 } mibSources
                     && MibModuleRegistryTemplating.CanEmit(existingTypes))
            {
                var registry = MibModuleRegistryTemplating.Generate(mibSources);
                ctx.AddSource(registry.HintName, registry.Source);
            }
        });
    }

    internal static bool IsEnabled(string? value) =>
        value is null
        || string.IsNullOrWhiteSpace(value)
        || !(value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase) || value.Trim() == "0");
}