
using System;
using Microsoft.CodeAnalysis;
using SnmpSharpNet.Mib.Ast;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Parlot;

using SourceSpan = Microsoft.CodeAnalysis.Text.TextSpan;
using Microsoft.CodeAnalysis.Text;

namespace SnmpSharpNet.Mib.SourceGenerator;

public record struct ContextlessMibModule(ModuleDefinition Module)
{
    public static Result<ContextlessMibModule> TryFromAdditionalText(AdditionalText text, CancellationToken ct)
    {
        var moduleHint = Path.GetFileNameWithoutExtension(text.Path);
        var source = text.GetText(ct)!;
        try
        {
            var module = MibParser.ParseAst(source.ToString(), moduleHint);
            return new ContextlessMibModule(module);
        }
        catch (ParseException e)
        {
            var pos = new LinePosition(e.Position.Line, e.Position.Column);
            var location = Location.Create(
                text.Path,
                new SourceSpan(e.Position.Offset, 0),
                new LinePositionSpan(pos, pos)
            );
            return Diagnostic.Create(Diagnostics.ParseError, location, e.Message);
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
