using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace SnmpSharpNet.Mib.Ast;

using Import = (IReadOnlyList<TextSpan> symbols, TextSpan fromModule);
using UnresolvedOid = (TextSpan parent, long oid);

// Common helpers for ASN.1 / Structure of Management Information Version 2
// See https://www.rfc-editor.org/rfc/rfc2578.html
public static class SMIv2 {
    // Whitespace parser that handles -- single-line comments globally
    public static readonly Parser<TextSpan> MibWhiteSpace =
        Capture(OneOf(
            Literals.WhiteSpace(includeNewLines: true),
            Literals.Comments("--")
        ).ZeroOrMany());

    // IDENT = [a-zA-Z][a-zA-Z0-9-_]*
    public static readonly Parser<TextSpan> Ident =
        Terms.Identifier(extraPart: c => c == '-' || c == '_');

    public static readonly Parser<IReadOnlyList<TextSpan>> Idents =
        Separated(Terms.Char(','), Ident);
    
    // Block[x] = "{" x "}"
    public static Parser<T> Block<T>(Parser<T> inner) =>
        Between(Terms.Char('{'), inner, Terms.Char('}'));

    // OidAssignment = "::=" "{" IDENT <number> "}"
    public static readonly Parser<UnresolvedOid> OidAssignment =
        Terms.Text("::=").ElseError("Expected '::=' in assignment")
            .SkipAnd(Terms.Char('{').ElseError("Expected '{' after '::='"))
            .SkipAnd(Ident.ElseError("Expected OID parent name"))
            .And(Terms.Integer().ElseError("Expected OID number"))
            .AndSkip(Terms.Char('}').ElseError("Expected '}'"));

    //
    // OBJECT-TYPE and NOTIFICATION-TYPE helpers
    //

    // SyntaxPart = "SYNTAX" <raw> $
    public static readonly Parser<TextSpan> SyntaxPart =
        Terms.Keyword("SYNTAX")
            .SkipAnd(Literals.WhiteSpace())
            .SkipAnd(AnyCharBefore(Literals.Char('\n')));

    // This is simply ignored
    // OTMaxAccessPart = "MAX-ACCESS" <string>
    public static readonly Parser<TextSpan> OTMaxAccessPart =
        Terms.Keyword("MAX-ACCESS")
            .SkipAnd(Ident);

    // OTStatusPart = "STATUS" ("current" | "deprecated" | "obsolete")
    public static readonly Parser<SMIv2Status> OTStatusPart =
        Terms.Keyword("STATUS").SkipAnd(OneOf(
                Terms.Keyword("current").Then(static x => SMIv2Status.Current),
                Terms.Keyword("deprecated").Then(static x => SMIv2Status.Deprecated),
                Terms.Keyword("obsolete").Then(static x => SMIv2Status.Obsolete)
        ).ElseError("Expected 'current', 'deprecated', or 'obsolete' after STATUS"));


    // This is simply ignored
    // OTDescriptionPart = "DESCRIPTION" <string>
    public static readonly Parser<TextSpan> OTDescriptionPart =
        Terms.Keyword("DESCRIPTION").SkipAnd(Terms.String().ElseError("Expected string after DESCRIPTION"));

    // This is simply ignored
    // OTReferPart = "REFERENCE" <string>
    public static readonly Parser<TextSpan> OTReferPart =
        Terms.Keyword("REFERENCE").SkipAnd(Terms.String());

    // This is simply ignored
    // OTUnitsPart = "UNITS" <string>
    public static readonly Parser<TextSpan> OTUnitsPart =
        Terms.Keyword("UNITS").SkipAnd(Terms.String());

    // Note: Currently we only return status
    //  OTMiddlePart =
    //      OTUnitsPart?
    //      OTMaxAccessPart
    //      OTStatusPart
    //      OTDescriptionPart
    //      OTReferPart?
    public static readonly Parser<SMIv2Status> OTMiddlePart =
        OTUnitsPart.ZeroOrOne()
            .AndSkip(OTMaxAccessPart)
            .SkipAnd(OTStatusPart)
            .AndSkip(OTDescriptionPart.ZeroOrOne())
            .AndSkip(OTReferPart.ZeroOrOne());
}
 
public enum SMIv2Status
{
    Current,
    Obsolete,
    Deprecated
}
public class ModuleDefinition(
    TextSpan identifier,
    IReadOnlyList<TextSpan> exports,
    IReadOnlyList<Import> imports,
    IReadOnlyList<ModuleItem> items
) {
    public TextSpan Identifier { get; } = identifier;
    public IReadOnlyList<TextSpan> Exports { get; } = exports;
    public IReadOnlyList<Import> Imports { get; } = imports;
    public IReadOnlyList<ModuleItem> Items { get; } = items;

    // Exports = "EXPORTS" [SymbolList] ";"
    public static readonly Parser<IReadOnlyList<TextSpan>> ExportsParser =
        Terms.Keyword("EXPORTS")
            .SkipAnd(Separated(Terms.Char(','), SMIv2.Ident))
            .AndSkip(Terms.Char(';'));

    // ImportsSymbolsFromModule = Symbol ("," Symbol)* "FROM" ModuleIdentifier
    static readonly Parser<Import> ImportsSymbolsFromModule =
        Separated(Terms.Char(','), SMIv2.Ident)
            .AndSkip(Terms.Keyword("FROM").ElseError("Expected 'FROM' after import symbols"))
            .And(SMIv2.Ident.ElseError("Expected module name after FROM"));

    // Imports = "IMPORTS" ImportsSymbolsFromModule* ";"
    public static readonly Parser<IReadOnlyList<Import>> ImportsParser =
        Terms.Keyword("IMPORTS")
            .SkipAnd(ImportsSymbolsFromModule.ZeroOrMany())
            .AndSkip(Terms.Char(';').ElseError("Expected ';' after IMPORTS"));

    // Module     = IDENT "DEFINITIONS" "::=" "BEGIN" ModuleBody? "END"
    // ModuleBody = Exports? Imports? ModuleItem*
    public static Parser<ModuleDefinition> Module =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("DEFINITIONS").ElseError("Expected 'DEFINITIONS' after module name"))
            .AndSkip(Terms.Text("::=").ElseError("Expected '::=' after DEFINITIONS"))
            .AndSkip(Terms.Keyword("BEGIN").ElseError("Expected 'BEGIN'"))
            .And(ExportsParser.Optional().Then(static x => x.OrSome([])))
            .And(ImportsParser.Optional().Then(static x => x.OrSome([])))
            .And(ModuleItem.Parser.ZeroOrMany())
            .AndSkip(Terms.Keyword("END").ElseError("Expected 'END' or a valid top-level definition"))
            .Then(static x => new ModuleDefinition(x.Item1, x.Item2, x.Item3, x.Item4));

    public static Parser<ModuleDefinition> EntryPoint = Module;

    public static ModuleDefinition Parse(Scanner scanner)
    {
        var context = new ParseContext(scanner)
        {
            WhiteSpaceParser = SMIv2.MibWhiteSpace
        };
        
        Module.Compile().TryParse(context, out var result, out var error);
        if (error is not null)
        {
            throw new FormatException($"Failed to parse MIB module @ {error?.Position}: '{error?.Message}'");
        }

        return result;
    }
};



public abstract class ModuleItem
{
    // ModuleItem = EntryDef
    //            | TextualConvention
    //            | ObjectGroup // Compliance, ignored
    //            | ModuleCompliance
    //            | ObjectIdentifier // Assignments 
    //            | ModuleIdentity
    //            | ConceptualTable
    //            | ConceptualRow
    //            | LeafObject
    public static readonly Parser<ModuleItem> Parser =
        OneOf<ModuleItem>(
            EntryDef.Parser,
            TextualConvention.Parser,
            MacroObjectGroup.InnerParser,
            MacroModuleCompliance.InnerParser,
            // Assignment
            ObjectIdentifier.InnerParser,
            ModuleIdentity.InnerParser,
            ConceptualTable.InnerParser,
            ConceptualRow.InnerParser,
            LeafObject.InnerParser
        );
}

public abstract class OidAssigner(
    TextSpan name,
    UnresolvedOid oid
) : ModuleItem {
    public TextSpan Name { get; } = name;
    public UnresolvedOid Oid { get; } = oid;
}

// A <EntryType> defintion in 7.1.12 "Conceptual Tables" of RFC2578
// https://www.rfc-editor.org/rfc/rfc2578.html#section-7.1.12
// OBJECT-TYPE with SYNTAX SEQUENCE OF <EntryType>
public class EntryDef(
    TextSpan name,
    IReadOnlyList<(TextSpan name, TextSpan type)> fields
) : ModuleItem {
    public TextSpan Name { get; } = name;
    public IReadOnlyList<(TextSpan name, TextSpan type)> Fields { get; } = fields;

    //  EntryDef = IDENT "::=" "SEQUENCE" "{"
    //      (RowEntryItem ",")* RowEntryItem
    //  "}"
    //  EntryDefItem = IDENT IDENT
    new public static readonly Parser<EntryDef> Parser =
        SMIv2.Ident
            .AndSkip(Terms.Text("::="))
            .AndSkip(Terms.Keyword("SEQUENCE"))
            .And(SMIv2.Block(
                Separated(Terms.Char(','), SMIv2.Ident.And(SMIv2.Ident))
            ))
            .Then(static x => new EntryDef(x.Item1, x.Item2));
}

// SNMP `TEXTUAL-CONVENTION`
// Basically a typedef with a display hint
// See https://www.rfc-editor.org/rfc/rfc2579.html#section-3
public class TextualConvention(
    TextSpan name,
    TextSpan syntax
) : ModuleItem {
    public TextSpan Name { get; } = name;
    public TextSpan Syntax { get; } = syntax;

    //  TextualConvention = IDENT "::=" "TEXTUAL-CONVENTION"
    //       ("DISPLAY-HINT" <string>)?
    //       STATUS <status>
    //       DESCRIPTION <string>
    //       (REFERENCE <string>)?
    //       SYNTAX <raw>
    new public static readonly Parser<TextualConvention> Parser =
        SMIv2.Ident
            .AndSkip(Terms.Text("::="))
            .AndSkip(Terms.Keyword("TEXTUAL-CONVENTION"))
            .AndSkip(Terms.Keyword("DISPLAY-HINT").AndSkip(Terms.String()).ZeroOrOne())
            .AndSkip(SMIv2.OTStatusPart)
            .AndSkip(SMIv2.OTDescriptionPart)
            .AndSkip(SMIv2.OTReferPart.ZeroOrOne())
            .And(Terms.Keyword("SYNTAX").SkipAnd(AnyCharBefore(Literals.Char('\n'))))
            .Then(static x => new TextualConvention(x.Item1, x.Item2));
}

// ASN.1 `OBJECT IDENTIFIER`
public class ObjectIdentifier(
    TextSpan name,
    UnresolvedOid oid
) : OidAssigner(name, oid) {
    // Note that this is a single keyword, even if it's 2 words.
    // See `ObjectIdentifierType` in section 32.1 of X.680 (ASN.1 spec)
    // ObjectIdentifier = IDENT "OBJECT" "IDENTIFIER" OidAssignment
    public static readonly Parser<ObjectIdentifier> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT"))
            .AndSkip(Terms.Keyword("IDENTIFIER"))
            .And(SMIv2.OidAssignment)
            .Then(static x => new ObjectIdentifier(x.Item1, x.Item2));
}

// SNMP `MODULE-IDENTITY`
public class ModuleIdentity(
    TextSpan name,
    UnresolvedOid oid,
    TextSpan lastUpdated,
    TextSpan organization,
    TextSpan contactInfo,
    TextSpan description,
    IReadOnlyList<(TextSpan, TextSpan)> revisions
) : OidAssigner(name, oid) {
    public TextSpan LastUpdated { get; } = lastUpdated;
    public TextSpan Organization { get; } = organization;
    public TextSpan ContactInfo { get; } = contactInfo;
    public TextSpan Description { get; } = description;
    public IReadOnlyList<(TextSpan, TextSpan)> Revisions { get; } = revisions;

    // Revision = "REVISION" <string> "DESCRIPTION" <string>
    static readonly Parser<(TextSpan, TextSpan)> Revision =
        Terms.Keyword("REVISION")
            .SkipAnd(Terms.String())
            .AndSkip(Terms.Keyword("DESCRIPTION"))
            .And(Terms.String());

    //  ModuleIdentity = "MODULE-IDENTITY"
    //      "LAST-UPDATED" <string>
    //      "ORGANIZATION" <string>
    //      "CONTACT-INFO" <string>
    //      "DESCRIPTION"  <string>
    //      Revision*
    public static readonly Parser<ModuleIdentity> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("MODULE-IDENTITY"))
            .AndSkip(Terms.Keyword("LAST-UPDATED").ElseError("Expected 'LAST-UPDATED' in MODULE-IDENTITY"))
            .And(Terms.String().ElseError("Expected date string after LAST-UPDATED"))
            .AndSkip(Terms.Keyword("ORGANIZATION").ElseError("Expected 'ORGANIZATION' in MODULE-IDENTITY"))
            .And(Terms.String().ElseError("Expected string after ORGANIZATION"))
            .AndSkip(Terms.Keyword("CONTACT-INFO").ElseError("Expected 'CONTACT-INFO' in MODULE-IDENTITY"))
            .And(Terms.String().ElseError("Expected string after CONTACT-INFO"))
            .AndSkip(Terms.Keyword("DESCRIPTION").ElseError("Expected 'DESCRIPTION' in MODULE-IDENTITY"))
            .And(Terms.String().ElseError("Expected string after DESCRIPTION"))
            .And(Revision.ZeroOrMany())
            .And(SMIv2.OidAssignment)
            .Then(static x => new ModuleIdentity(
                x.Item1, x.Item7,
                x.Item2, x.Item3, x.Item4, x.Item5, x.Item6));
}

// A "Conceptual Table" in 7.1.12 "Conceptual Tables" of RFC2578
// https://www.rfc-editor.org/rfc/rfc2578.html#section-7.1.12
// OBJECT-TYPE with SYNTAX SEQUENCE OF <EntryType>
public class ConceptualTable(
    TextSpan name,
    UnresolvedOid oid,
    TextSpan entryType,
    SMIv2Status status
) : OidAssigner(name, oid) {
    public TextSpan EntryType { get; } = entryType;
    public SMIv2Status Status { get; } = status;

    //  ConceptualTable = "OBJECT-TYPE"
    //      "SYNTAX" "SEQUENCE" "OF" <ident>
    //      OTMiddlePart
    public static readonly Parser<ConceptualTable> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-TYPE"))
            .AndSkip(Terms.Keyword("SYNTAX"))
            .AndSkip(Terms.Keyword("SEQUENCE"))
            .AndSkip(Terms.Keyword("OF"))
            .And(SMIv2.Ident)
            .And(SMIv2.OTMiddlePart)
            .And(SMIv2.OidAssignment)
            .Then(static x => new ConceptualTable(
                x.Item1, x.Item4, x.Item2, x.Item3));
}

// A "Conceptual Row" in 7.1.12 "Conceptual Tables" of RFC2578
// https://www.rfc-editor.org/rfc/rfc2578.html#section-7.1.12
// OBJECT-TYPE with SYNTAX <EntryType> and INDEX/AUGMENTS
public class ConceptualRow(
    TextSpan name,
    UnresolvedOid oid,
    TextSpan entryType,
    SMIv2Status status,
    IReadOnlyList<TextSpan> index
) : OidAssigner(name, oid) {
    public TextSpan EntryType { get; } = entryType;
    public SMIv2Status Status { get; } = status;
    public IReadOnlyList<TextSpan> Index { get; } = index;

    // In official syntax `IndexPart` is can always be added to a `OBJECT-TYPE`
    // macro and it comes after `ReferPart` but before `DefValPart`.
    // Semantically however it must be present iff it's "Conceptual Row" so here
    // we only add the syntax to "Conceptual Row"
    // See "7.7.  Mapping of the INDEX clause" of RFC2578
    // https://www.rfc-editor.org/rfc/rfc2578.html#section-7.7
    //
    // Note: "AUGMENTS" and "IMPLIED" is not implemented
    // OTIndexPart = "INDEX"    "{" IndexTypes "}"
    //             | "AUGMENTS" "{" Entry      "}"
    // OTIndexItems = (IDENT ",")* "IMPLIED" IDENT
    //              | (IDENT ",")* IDENT
    public static readonly Parser<IReadOnlyList<TextSpan>> ObjectTypeIndexPart =
        Terms.Keyword("INDEX")
            .SkipAnd(SMIv2.Block(SMIv2.Idents));

    //  ConceptualRow = "OBJECT-TYPE"
    //      "SYNTAX" <ident>
    //      "MAX-ACCESS" not-accessible
    //      "STATUS" <ident>
    //      ["DESCRIPTION" <string>]
    //      ["REFERENCE" <string>]
    //      ["INDEX" "{" ident, ... "}"]
    public static readonly Parser<ConceptualRow> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-TYPE"))
            .AndSkip(Terms.Keyword("SYNTAX"))
            .And(SMIv2.Ident)
            .And(SMIv2.OTMiddlePart)
            .And(ObjectTypeIndexPart)
            .And(SMIv2.OidAssignment)
            .Then(static x => new ConceptualRow(
                x.Item1, x.Item5, x.Item2, x.Item3, x.Item4));
}

// OBJECT-TYPE for scalar/columnar objects
public class LeafObject(
    TextSpan name,
    UnresolvedOid oid,
    TextSpan syntax,
    SMIv2Status status
) : OidAssigner(name, oid) {
    public TextSpan Syntax { get; } = syntax;
    public SMIv2Status Status { get; } = status;

    // This is simply ignored
    // DefValPart = "DEFVAL" "{" <raw> "}"
    public static readonly Parser<TextSpan> DefValPart =
        Terms.Keyword("DEFVAL")
            .SkipAnd(SMIv2.Block(AnyCharBefore(Terms.Char('}'))));

    // LeafObject = IDENT "OBJECT-TYPE" SyntaxPart OTMiddlePart DefValPart OidAssignment
    public static readonly Parser<LeafObject> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-TYPE"))
            .And(SMIv2.SyntaxPart)
            .And(SMIv2.OTMiddlePart)
            .AndSkip(DefValPart.ZeroOrOne())
            .And(SMIv2.OidAssignment)
            .Then(static x => new LeafObject(
                x.Item1, x.Item4, x.Item2, x.Item3));
}

// SNMP `OBJECT-GROUP`
// Note: Since we don't care about compliance, we cheat when parsing this
public class MacroObjectGroup : ModuleItem {
    private static readonly MacroObjectGroup instance = new();

    //  ObjectGroup = IDENT "OBJECT-GROUP"
    //      "OBJECTS" "{" <raw> "}"
    //      "STATUS" <status>
    //      "DESCRIPTION" <string>
    //      ("REFERENCE" <string>)?
    //      "::=" "{" <raw> "}"
    public static readonly Parser<MacroObjectGroup> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-GROUP"))
            .AndSkip(AnyCharBefore(Literals.Text("::="), consumeDelimiter: true))
            .AndSkip(SMIv2.Block(AnyCharBefore(Terms.Char('}'))))
            .Then(static x => instance);
}

// SNMP `MODULE-COMPLIANCE` macro
// Note: Since we don't care about compliance, we cheat when parsing this
public class MacroModuleCompliance : ModuleItem {
    private static readonly MacroModuleCompliance instance = new();

    //  ModuleCompliance = IDENT "MODULE-COMPLIANCE"
    //      "STATUS" <status>
    //      "DESCRIPTION" <string>
    //      ("REFERENCE" <string>)?
    //      MCModulePart+
    //      "::=" "{" IDENT <number> "}"
    //  MCModulePart = "MODULE" IDENT?
    //      ("MANDATORY-GROUPS" "{" <raw> "}")?
    //      (ComplianceGroup | ComplianceObject)*
    //  MCGroup = "GROUP" IDENT "DESCRIPTION" <string>
    //  MCObject = "OBJECT" IDENT
    //     ("SYNTAX" <raw>)?
    //     ("WRITE-SYNTAX" <raw>)?
    //     ("MIN-ACCESS" IDENT)?
    //     "DESCRIPTION" <string>
    public static readonly Parser<MacroModuleCompliance> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("MODULE-COMPLIANCE"))
            .AndSkip(AnyCharBefore(Literals.Text("::="), consumeDelimiter: true))
            .AndSkip(SMIv2.Block(AnyCharBefore(Terms.Char('}'))))
            .Then(static x => instance);
}
