
using System;
using Microsoft.CodeAnalysis;
using SnmpSharpNet.Mib.Ast;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Parlot;

namespace SnmpSharpNet.Mib.SourceGenerator;

public record struct GenMibDefinition(
    string Name, Location Location,
    Result<ModuleDefinition> Module
) {
    public static GenMibDefinition FromAdditionalText(AdditionalText text, CancellationToken ct)
    {
        var name = Path.GetFileNameWithoutExtension(text.Path);
        var loc = Location.FromAdditionalText(text);
        var source = text.GetText(ct)!;

        Result<ModuleDefinition> module;
        try
        {
            var mod = MibParser.ParseAst(source.ToString(), name);
            if (mod.Identifier == name)
            {
                module = mod;
            }
            else
            {
                module = Diagnostic.Create(Diagnostics.ParseError, loc, $"Parsed '{name}.mib' but got module '{mod.Identifier.ToString()}'");
            }

        }
        catch (ParseException e)
        {
            module = Diagnostic.Create(Diagnostics.ParseError, loc.WithPosition(e.Position), e.Message);
        }
        return new GenMibDefinition(name, loc, module);
    }

    public readonly bool CollectError(List<Diagnostic> errors)
    {
        if (Module.IsOk)
        {
            return false;
        }
        else
        {
            errors.Add(Module.Diagnostic);
            return true;
        }
    }
}



public class ValuedDictionary<T, U> : Dictionary<T, U>, IEquatable<ValuedDictionary<T, U>>
    where T : IEquatable<T>
    where U : IEquatable<U>
{
    public ValuedDictionary() {}
    public ValuedDictionary(IDictionary<T, U> dictionary) : base(dictionary) {}

    public bool Equals(ValuedDictionary<T, U>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Count == other.Count && !this.Except(other).Any();
    }

    public override bool Equals(object? obj) => Equals(obj as ValuedDictionary<T, U>);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 0;
            foreach (var kvp in this)
                hash += HashCode.Combine(kvp.Key, kvp.Value);
            return hash;
        }
    }
}



public record struct PartialClass(string Accessibility, string? Namespace, string TypeName, Location Location)
{
    public static PartialClass FromSymbol(ISymbol symbol)
    {
        var accessibility = symbol.DeclaredAccessibility switch {
            Microsoft.CodeAnalysis.Accessibility.Public => "public",
            Microsoft.CodeAnalysis.Accessibility.Private => "private",
            Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
            Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
            Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
            Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
            _ => "private"
        };

        var cns = symbol.ContainingNamespace;
        var ns = cns.IsGlobalNamespace ? null : cns.ToDisplayString();
        return new(accessibility, ns, symbol.Name, symbol.Locations.First());
    }

    public readonly string[] AsLiteral(string impls, IEnumerable<string> body)
    {
        string[] decl = [
            $"{Accessibility} partial class {TypeName}{impls}",
            $"{{",
            ..body.Select(x => $"\t{x}"),
            $"}}",
        ];
        if (Namespace is {})
        {
            return [
                $"namespace {Namespace}",
                $"{{",
                ..decl.Select(x => $"\t{x}"),
                $"}}"
            ];
        }
        else
        {
            return decl;
        }
    }
}
