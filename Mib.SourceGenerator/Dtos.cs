
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
    Result<ModuleDefinition> Module,
    IReadOnlyList<Diagnostic>? Warnings
) {
    public static GenMibDefinition FromAdditionalText(AdditionalText text, CancellationToken ct)
    {
        var name = Path.GetFileNameWithoutExtension(text.Path);
        var loc = Location.FromAdditionalText(text);
        var source = text.GetText(ct)!;

        Result<ModuleDefinition> module;
        List<Diagnostic>? warnings = null;
        try
        {
            var mod = MibParser.ParseAst(source.ToString(), name);
            if (mod.Identifier != name)
            {
                // The filename doesn't match the module identifier.
                // Use the parsed identifier as the canonical name and emit a warning
                // so that MIBs shipped in packages with arbitrary filenames still resolve.
                warnings = [Diagnostic.Create(
                    Diagnostics.FilenameMismatch, loc,
                    $"File '{name}.mib' contains module '{mod.Identifier}'; using module identifier as key.")];
                name = mod.Identifier.ToString();
            }
            module = mod;
        }
        catch (ParseException e)
        {
            module = Diagnostic.Create(Diagnostics.ParseError, loc.WithPosition(e.Position), e.Message);
        }
        return new GenMibDefinition(name, loc, module, warnings);
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
