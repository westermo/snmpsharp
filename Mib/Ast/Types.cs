using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace SnmpSharpNet.Mib.Ast;

public class Refinement(IReadOnlyList<(long, Option<long>)> contraints, bool isSize = false)
{
    public IReadOnlyList<(long, Option<long>)> Contraints => contraints;
    public bool IsSize = isSize;

    public Refinement AsSizeRefinement()
    {
        return new Refinement(contraints, true);
    }

    public static Refinement AllValues = new([]);
    public static Refinement AllSizes = new([], true);
}

public class Type
{
    // Based on https://www.rfc-editor.org/rfc/rfc2578.html#section-11.1
    // TypeRefinementValue = "(" Range ( "|" Range )* ")"
    // Range = <number> | <number> ".." <number>
    static readonly Parser<(long, Option<long>)> _range =
        Terms.Integer()
            .And(Terms.Text("..").SkipAnd(Terms.Integer()).Optional());
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

    // Type = Integer32Enum // Uses same "INTEGER" keyword as Integer32
    //      | Integer32 | Unsigned32
    //      | OCTET STRING
    //      | OBJECT IDENTIFIER
    //      | BITS
    //      | Counter32 | Counter64
    //      | Gauge32
    //      | TimeTicks
    public static readonly Parser<Type> Parser =
        OneOf(
            Integer32Enum.TypeEnum.Then(x => (Type)x),
            Integer32.TypeInteger32.Then(x => (Type)x),
            Unsigned32.TypeUnsigned32.Then(x => (Type)x),
            OctetString.TypeOctetString.Then(x => (Type)x),
            ObjectIdentifierType.TypeObjectIdentifier.Then(x => (Type)x),
            Bits.TypeBits.Then(x => (Type)x),
            Counter32.TypeCounter32.Then(x => (Type)x),
            Counter64.TypeCounter64.Then(x => (Type)x),
            Gauge32.TypeGauge32.Then(x => (Type)x),
            TimeTicks.TypeTimeTicks.Then(x => (Type)x),
            TextualConventionType.TypeTextualConvention.Then(x => x)
        );
}

// §7.1.1 — enumeration first to resolve INTEGER ambiguity
public class Integer32Enum(IReadOnlyList<(TextSpan, long)> values) : Type
{
    public IReadOnlyList<(TextSpan, long)> values = values;

    // TypeEnum = "INTEGER" TypeRefinementEnum
    public static readonly Parser<Integer32Enum> TypeEnum =
        Terms.Keyword("INTEGER")
            .SkipAnd(TypeRefinementEnum)
            .Then(x => new Integer32Enum(x));
}

// §7.1.1
public class Integer32(Refinement refinement) : Type
{
    public Refinement refinement = refinement;

    // TypeInteger32 = "Integer32" | "INTEGER" TypeRefinementValue?
    public static readonly Parser<Integer32> TypeInteger32 =
        Terms.Keyword("INTEGER").Or(Terms.Keyword("Integer32"))
            .SkipAnd(TypeRefinementValue.Optional())
            .Then(x => new Integer32(x.OrSome(Refinement.AllValues)));
}

// §7.1.11
public class Unsigned32(Refinement refinement) : Type
{
    public Refinement refinement = refinement;

    // TypeUnsigned32 = "Unsigned32" TypeRefinementValue?
    public static readonly Parser<Unsigned32> TypeUnsigned32 =
        Terms.Keyword("Unsigned32")
            .SkipAnd(TypeRefinementValue.Optional())
            .Then(x => new Unsigned32(x.OrSome(Refinement.AllValues)));
}

// §7.1.2
public class OctetString(Refinement refinement) : Type
{
    public Refinement Refinemnet = refinement;

    // TypeOctetString = "OCTET" "STRING" TypeRefinementSize?
    public static readonly Parser<OctetString> TypeOctetString =
        Terms.Keyword("OCTET")
            .SkipAnd(Terms.Keyword("STRING"))
            .SkipAnd(TypeRefinementSize.Optional())
            .Then(x => new OctetString(x.OrSome(Refinement.AllSizes)));
}

// §7.1.3
public class ObjectIdentifierType : Type
{
    // TypeObjectIdentifier = "OBJECT" "IDENTIFIER"
    public static readonly Parser<ObjectIdentifierType> TypeObjectIdentifier =
        Terms.Keyword("OBJECT")
            .AndSkip(Terms.Keyword("IDENTIFIER"))
            .Then(_ => new ObjectIdentifierType());
}

// §7.1.4
public class Bits(IReadOnlyList<(TextSpan, long)> values) : Type
{
    public IReadOnlyList<(TextSpan, long)> values = values;

    // TypeBits = "BITS" TypeRefinementEnum
    public static readonly Parser<Bits> TypeBits =
        Terms.Keyword("BITS")
            .SkipAnd(TypeRefinementEnum)
            .Then(x => new Bits(x));
}

// §7.1.6
public class Counter32 : Type
{
    // TypeCounter32 = "Counter32"
    public static readonly Parser<Counter32> TypeCounter32 =
        Terms.Keyword("Counter32")
            .Then(_ => new Counter32());
}

// §7.1.10
public class Counter64 : Type
{
    // TypeCounter64 = "Counter64"
    public static readonly Parser<Counter64> TypeCounter64 =
        Terms.Keyword("Counter64")
            .Then(_ => new Counter64());
}

// §7.1.7
public class Gauge32(Refinement refinement) : Type
{
    public Refinement refinement = refinement;

    // TypeGauge32 = "Gauge32" TypeRefinementValue?
    public static readonly Parser<Gauge32> TypeGauge32 =
        Terms.Keyword("Gauge32")
            .SkipAnd(TypeRefinementValue.Optional())
            .Then(x => new Gauge32(x.OrSome(Refinement.AllValues)));
}

// §7.1.8
public class TimeTicks : Type
{
    // TypeTimeTicks = "TimeTicks"
    public static readonly Parser<TimeTicks> TypeTimeTicks =
        Terms.Keyword("TimeTicks")
            .Then(_ => new TimeTicks());
}

public class TextualConventionType(TextSpan name) : Type
{
    public TextSpan name = name;

    // TypeTextualConvention = IDENT TypeRefinementValue
    public static readonly Parser<TextualConventionType> TypeTextualConvention =
        SMIv2.Ident
            .And(TypeRefinementValue.Or(TypeRefinementSize).Optional())
            .Then(x => new TextualConventionType(x.Item1));
}
