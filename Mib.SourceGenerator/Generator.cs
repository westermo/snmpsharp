using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

using AllMibs = (ValuedDictionary<string, MibModule>?, ImmutableArray<Diagnostic>?);

[Generator]
public sealed class SnmpGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
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

                var modules = asts.ConstructModules();
                if (!modules.IsOk)
                {
                    errors.Add(modules.Diagnostic);
                }

                if (errors.Count > 0)
                {
                    return (null, errors.ToImmutableArray());
                }

                // Return warnings together with the successful result so they can be emitted
                var allDiagnostics = warnings.ToImmutableArray();
                return (new ValuedDictionary<string, MibModule>(modules.Value),
                    allDiagnostics.Length > 0 ? allDiagnostics : null);
            });

        context.RegisterSourceOutput(context.CompilationProvider.Combine(allMibs), (ctx, x) =>
        {
            var compilation = x.Left;
            var mibs = x.Right;

            if (mibs.Item2 is {} diagnostics)
            {
                foreach (var diag in diagnostics)
                {
                    ctx.ReportDiagnostic(diag);
                }
                // Abort on errors (warnings still reported above, module is null for errors)
                if (mibs.Item1 is null) { return; }
            }

            var tree = OidTreeBuilder.Build(mibs.Item1!);
            foreach (var source in TreeTemplating.Generate(tree, compilation))
            {
                ctx.AddSource(source.HintName, source.Source);
            }
        });
    }
}
