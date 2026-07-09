using System;
using System.Collections.Generic;
using System.Linq;
using Parlot;
using Parlot.Fluent;
using SnmpSharpNet.Mib;
using static Parlot.Fluent.Parsers;

namespace SnmpSharpNet.Mib.Ast;

public class AstType(
    TypeKind? kind,
    Refinement? refinement = null,
    IReadOnlyList<(TextSpan, long)>? values = null,
    TextSpan? name = null
) {
    public TypeKind? Kind { get; } = kind;
    public Refinement? Refinement { get; } = refinement;
    public IReadOnlyList<(TextSpan, long)>? Values { get; } = values;
    public TextSpan? Name { get; } = name;

    // Based on https://www.rfc-editor.org/rfc/rfc2578.html#section-11.1
    // TypeRefinementValue = "(" Range ( "|" Range )* ")"
    // Range = <number> | <number> ".." <number>
    static readonly Parser<(int Min, int Max)> _range =
        Terms.Integer()
            .And(Terms.Text("..").SkipAnd(Terms.Integer()).Optional())
            .Then(x => {
                var min = x.Item1;
                var max = x.Item2.OrSome(x.Item1);
                return (checked((int)min), checked((int)max));
            });
    public static readonly Parser<Refinement> TypeRefinementValue =
        Terms.Char('(')
            .SkipAnd(Separated(Terms.Char('|'), _range))
            .AndSkip(Terms.Char(')'))
            .Then(x => new Refinement(x));
    
    // TypeSizeRefinement = "(" "SIZE" TypeRefinementValue ")"
    public static readonly Parser<Refinement> TypeRefinementSize =
        Terms.Char('(')
            .SkipAnd(Terms.Keyword("SIZE"))
            .SkipAnd(TypeRefinementValue)
            .Then(x => new Refinement(x.Contraints, true))
            .AndSkip(Terms.Char(')'));

    // TypeRefinementEnum = "{" IDENT "(" <number> ")" ( "," IDENT "(" <number> ")" )* "}"
    public static readonly Parser<IReadOnlyList<(TextSpan, long)>> TypeRefinementEnum =
        Terms.Char('{')
            .SkipAnd(
                Separated(
                    Terms.Char(','),
                    SMIv2.Ident
                        .AndSkip(Terms.Char('('))
                        .And(Terms.Integer())
                        .AndSkip(Terms.Char(')'))
                )
            )
            .AndSkip(Terms.Char('}'));

    // TypeInteger32Enum = "INTEGER" TypeRefinementEnum
    static readonly Parser<AstType> TypeInteger32Enum =
        Terms.Keyword("INTEGER")
            .SkipAnd(TypeRefinementEnum)
            .Then(x => new AstType(TypeKind.Integer32Enum, values: x));

    // TypeInteger32 = "Integer32" | "INTEGER" TypeRefinementValue?
    static readonly Parser<AstType> TypeInteger32 =
        Terms.Keyword("INTEGER").Or(Terms.Keyword("Integer32"))
            .SkipAnd(TypeRefinementValue.Optional())
            .Then(x => new AstType(TypeKind.Integer32, refinement: x.OrSome(Refinement.AllValues)));

    // TypeUnsigned32 = "Unsigned32" TypeRefinementValue?
    static readonly Parser<AstType> TypeUnsigned32 =
        Terms.Keyword("Unsigned32")
            .SkipAnd(TypeRefinementValue.Optional())
            .Then(x => new AstType(TypeKind.Unsigned32, refinement: x.OrSome(Refinement.AllValues)));

    // TypeOctetString = "OCTET" "STRING" TypeRefinementSize?
    static readonly Parser<AstType> TypeOctetString =
        Terms.Keyword("OCTET")
            .SkipAnd(Terms.Keyword("STRING"))
            .SkipAnd(TypeRefinementSize.Optional())
            .Then(x => new AstType(TypeKind.OctetString, refinement: x.OrSome(Refinement.AllSizes)));

    // TypeIpAddress = "IpAddress"
    static readonly Parser<AstType> TypeIpAddress =
        Terms.Keyword("IpAddress")
            .Then(_ => new AstType(TypeKind.IpAddress));

    // TypeObjectIdentifier = "OBJECT" "IDENTIFIER"
    static readonly Parser<AstType> TypeObjectIdentifier =
        Terms.Keyword("OBJECT")
            .AndSkip(Terms.Keyword("IDENTIFIER"))
            .Then(_ => new AstType(TypeKind.ObjectIdentifier));

    // TypeBits = "BITS" TypeRefinementEnum
    static readonly Parser<AstType> TypeBits =
        Terms.Keyword("BITS")
            .SkipAnd(TypeRefinementEnum)
            .Then(x => new AstType(TypeKind.Bits, values: x));

    // TypeOpaque = "Opaque"
    static readonly Parser<AstType> TypeOpaque =
        Terms.Keyword("Opaque")
            .Then(_ => new AstType(TypeKind.Opaque));

    // TypeCounter32 = "Counter32"
    static readonly Parser<AstType> TypeCounter32 =
        Terms.Keyword("Counter32")
            .Then(_ => new AstType(TypeKind.Counter32));

    // TypeCounter64 = "Counter64"
    static readonly Parser<AstType> TypeCounter64 =
        Terms.Keyword("Counter64")
            .Then(_ => new AstType(TypeKind.Counter64));

    // TypeGauge32 = "Gauge32" TypeRefinementValue?
    static readonly Parser<AstType> TypeGauge32 =
        Terms.Keyword("Gauge32")
            .SkipAnd(TypeRefinementValue.Optional())
            .Then(x => new AstType(TypeKind.Gauge32, refinement: x.OrSome(Refinement.AllValues)));

    // TypeTimeTicks = "TimeTicks"
    static readonly Parser<AstType> TypeTimeTicks =
        Terms.Keyword("TimeTicks")
            .Then(_ => new AstType(TypeKind.TimeTicks));

    // TypeTextualConvention = IDENT TypeRefinementValue?
    static readonly Parser<AstType> TypeTextualConvention =
        SMIv2.Ident
            .And(TypeRefinementValue.Or(TypeRefinementSize).Optional())
            .Then(x => new AstType(null, refinement: x.Item2.OrSome(null), name: x.Item1));

    // Type = Integer32Enum // Uses same "INTEGER" keyword as Integer32
    //      | Integer32 | Unsigned32
    //      | OCTET STRING
    //      | IpAddress
    //      | OBJECT IDENTIFIER
    //      | Opaque
    //      | BITS
    //      | Counter32 | Counter64
    //      | Gauge32
    //      | TimeTicks
    //      | TextualConvention
    public static readonly Parser<AstType> Parser =
        OneOf(
            TypeInteger32Enum,
            TypeInteger32,
            TypeUnsigned32,
            TypeOctetString,
            TypeIpAddress,
            TypeObjectIdentifier,
            TypeOpaque,
            TypeBits,
            TypeCounter32,
            TypeCounter64,
            TypeGauge32,
            TypeTimeTicks,
            TypeTextualConvention
        );

    public override string ToString() =>
        Kind switch
        {
            TypeKind.Integer32Enum => $"INTEGER {{{string.Join(", ", Values?.Cast<(TextSpan, long)>().Select(v => $"{v.Item1}({v.Item2})") ?? [])}}}",
            TypeKind.Integer32 => Refinement?.Contraints.Count > 0 ? $"INTEGER {Refinement}" : "INTEGER",
            TypeKind.Unsigned32 => Refinement?.Contraints.Count > 0 ? $"Unsigned32 {Refinement}" : "Unsigned32",
            TypeKind.OctetString => Refinement?.IsSize == true ? $"OCTET STRING {Refinement}" : "OCTET STRING",
            TypeKind.IpAddress => "IpAddress",
            TypeKind.ObjectIdentifier => "OBJECT IDENTIFIER",
            TypeKind.Opaque => "Opaque",
            TypeKind.Bits => $"BITS {{{string.Join(", ", Values?.Cast<(TextSpan, long)>().Select(v => $"{v.Item1}({v.Item2})") ?? [])}}}",
            TypeKind.Counter32 => "Counter32",
            TypeKind.Counter64 => "Counter64",
            TypeKind.Gauge32 => Refinement?.Contraints.Count > 0 ? $"Gauge32 {Refinement}" : "Gauge32",
            TypeKind.TimeTicks => "TimeTicks",
            null => Name?.ToString() ?? "?",
            _ => "Unknown"
        };

    public override bool Equals(object? obj)
    {
        if (obj is not AstType other) return false;
        
        return Kind == other.Kind
            && Equals(Refinement, other.Refinement)
            && (Values ?? []).SequenceEqual(other.Values ?? [])
            && Name == other.Name;
    }

    public override int GetHashCode() =>
        Kind.GetHashCode()
            ^ Refinement?.GetHashCode() ?? 0
            ^ Values?.Count ?? 0
            ^ Name?.GetHashCode() ?? 0;
}
