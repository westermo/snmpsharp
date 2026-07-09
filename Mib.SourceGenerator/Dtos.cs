
using System;
using Microsoft.CodeAnalysis;
using SnmpSharpNet.Mib.Ast;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace SnmpSharpNet.Mib.SourceGenerator;

public record struct ContextlessMibModule(ModuleDefinition Module)
{
    public static Result<ContextlessMibModule> TryFromAdditionalText(AdditionalText text, CancellationToken ct)
    {
        var moduleHint = Path.GetFileNameWithoutExtension(text.Path);
        try
        {
            var module = MibParser.ParseAst(text.GetText(ct)!.ToString(), moduleHint);
            return new ContextlessMibModule(module);
        }
        catch (FormatException e)
        {
            return Diagnostic.Create(Diagnostics.ParseError, Location.None, e.Message);
        }
    }
}

public class ValuedDictionary<T, U> : Dictionary<T, U>, IEquatable<ValuedDictionary<T, U>>
    where T : IEquatable<T>
    where U : IEquatable<U>
{
    public bool Equals(ValuedDictionary<T, U> other)
    {
        return Count == other.Count && !this.Except(other).Any();;
    }
}
