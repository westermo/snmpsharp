using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mib.Ast;
using Parlot;
using Parlot.Compilation;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace SnmpSharpNet.Mib.Ast;

using Import = (IReadOnlyList<TextSpan> symbols, TextSpan fromModule);


public enum SMIv2Status
{
    Current,
    Obsolete,
    Deprecated
}

public enum SMIv2Accessibility
{
    NotAccessible,
    AccessibleForNotify,
    ReadOnly,
    ReadWrite,
    ReadCreate
}

public static class SMIv2AccessibilityExtensions
{
    extension(SMIv2Accessibility accessibility)
    {
        public bool CanRead() =>
            accessibility >= SMIv2Accessibility.ReadOnly;
    }
}


// Common helpers for ASN.1 / Structure of Management Information Version 2
// See https://www.rfc-editor.org/rfc/rfc2578.html
public static class SMIv2 {
    // Whitespace parser that handles -- single-line comments globally
    public static readonly Parser<TextSpan> MibWhiteSpace =
        Capture(OneOf(
            Literals.WhiteSpace(includeNewLines: true),
            Literals.Comments("--")
        ).ZeroOrMany());

    // ASN.1 string: everything between double quotes, no escape handling
    public static readonly Parser<TextSpan> String =
        SkipWhiteSpace(
            Literals.Char('"')
                .SkipAnd(AnyCharBefore(Literals.Char('"'), canBeEmpty: true, consumeDelimiter: true))
        );

    // IDENT = [a-zA-Z][a-zA-Z0-9-_]*
    public static readonly Parser<TextSpan> Ident =
        Terms.Identifier(extraPart: c => c == '-' || c == '_').WithName("Ident");

    public static readonly Parser<IReadOnlyList<TextSpan>> Idents =
        Separated(Terms.Char(','), Ident).WithName("Idents");
    
    // Block[x] = "{" x "}"
    public static Parser<T> Block<T>(Parser<T> inner) =>
        Between(Terms.Char('{'), inner, Terms.Char('}')).WithName("Block");

    // OidAssignment = "::=" "{" IDENT ("(" <number> ")")? ( IDENT "(" <number> ")" )* <number> "}"
    public static readonly Parser<UnresolvedOid> OidAssignment =
        Terms.Text("::=").ElseError("Expected '::='")
            .SkipAnd(Terms.Char('{').ElseError("Expected '{' after '::='"))
            .SkipAnd(Ident.ElseError("Expected OID parent name"))
            .AndSkip(
                // Skip optional (number) on the parent ident (e.g. iso(1))
                Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')')).Optional()
            )
            .And(
                Ident
                .AndSkip(Terms.Char('(').ElseError("Expected '(' after OID component name"))
                .And(Terms.Integer().ElseError("Expected OID number"))
                .AndSkip(Terms.Char(')').ElseError("Expected ')' after OID component number"))
                .ZeroOrMany()
            )
            .And(Terms.Integer().ElseError("Expected OID number"))
            .AndSkip(Terms.Char('}').ElseError("Expected '}'"))
            .Then(static x => new UnresolvedOid(x.Item1, x.Item2, x.Item3))
            .WithName("OidAssignment");

    //
    // OBJECT-TYPE and NOTIFICATION-TYPE helpers
    //

    // OTSyntaxPart = "SYNTAX" Type
    public static readonly Parser<AstType> OTSyntaxPart =
        Terms.Keyword("SYNTAX")
            .SkipAnd(AstType.Parser.ElseError("Expected type after SYNTAX"))
            .WithName("OTSyntaxPart");

    // OTMaxAccessPart = "MAX-ACCESS" AccessSpecifier
    // AccessSpecifier = "not-accessible"
    //                 | "accessible-for-notify"
    //                 | "read-only"
    //                 | "read-write"
    //                 | "read-create"
    public static readonly Parser<SMIv2Accessibility> OTMaxAccessPart =
        Terms.Keyword("MAX-ACCESS")
            .SkipAnd(
                OneOf(
                    Terms.Keyword("not-accessible").Then(static x => SMIv2Accessibility.NotAccessible),
                    Terms.Keyword("accessible-for-notify").Then(static x => SMIv2Accessibility.AccessibleForNotify),
                    Terms.Keyword("read-only").Then(static x => SMIv2Accessibility.ReadOnly),
                    Terms.Keyword("read-write").Then(static x => SMIv2Accessibility.ReadWrite),
                    Terms.Keyword("read-create").Then(static x => SMIv2Accessibility.ReadCreate)
                ).ElseError("Expected 'not-accessible', 'accessible-for-notify', 'read-only', 'read-write' or 'read-create' after MAX-ACCESS")
            ).WithName("OTMaxAccessPart");

    // OTStatusPart = "STATUS" ("current" | "deprecated" | "obsolete")
    public static readonly Parser<SMIv2Status> OTStatusPart =
        Terms.Keyword("STATUS")
            .SkipAnd(
                OneOf(
                    Terms.Keyword("current").Then(static x => SMIv2Status.Current),
                    Terms.Keyword("deprecated").Then(static x => SMIv2Status.Deprecated),
                    Terms.Keyword("obsolete").Then(static x => SMIv2Status.Obsolete)
                ).ElseError("Expected 'current', 'deprecated', or 'obsolete' after STATUS")
            ).WithName("OTStatusPart");


    // This is simply ignored
    // OTDescriptionPart = "DESCRIPTION" <string>
    public static readonly Parser<TextSpan> OTDescriptionPart =
        Terms.Keyword("DESCRIPTION")
            .SkipAnd(String.ElseError("Expected string after DESCRIPTION"))
            .WithName("OTDescriptionPart");

    // This is simply ignored
    // OTReferPart = "REFERENCE" <string>
    public static readonly Parser<TextSpan> OTReferPart =
        Terms.Keyword("REFERENCE")
            .SkipAnd(String.ElseError("Expected string after REFERENCE"))
            .WithName("OTReferPart");

    // This is simply ignored
    // OTUnitsPart = "UNITS" <string>
    public static readonly Parser<TextSpan> OTUnitsPart =
        Terms.Keyword("UNITS")
            .SkipAnd(String.ElseError("Expected string after UNITS"))
            .WithName("OTUnitsPart");

    // Note: Currently we only return max-access, status and description
    //  OTMiddlePart =
    //      OTUnitsPart?
    //      OTMaxAccessPart
    //      OTStatusPart
    //      OTDescriptionPart
    //      OTReferPart?
    public static readonly Parser<(SMIv2Accessibility, SMIv2Status, TextSpan?)> OTMiddlePart =
        OTUnitsPart.ZeroOrOne()
            .SkipAnd(OTMaxAccessPart.ElseError("Expected 'MAX-ACCESS' in object type"))
            .And(OTStatusPart.ElseError("Expected 'STATUS' in object type"))
            .And(OTDescriptionPart.ZeroOrOne().Then(static x => x.Length == 0 ? (TextSpan?)null : x))
            .AndSkip(OTReferPart.ZeroOrOne())
            .Then(static x => (x.Item1, x.Item2, x.Item3))
            .WithName("OTMiddlePart");
}


 
public class UnresolvedOid(
    TextSpan parent,
    IReadOnlyList<(TextSpan Name, long Number)> components,
    long oid
) {
    public TextSpan Parent { get; } = parent;
    public IReadOnlyList<(TextSpan Name, long Number)> Components { get; } = components;
    public long Oid { get; } = oid;
};

public class ModuleDefinition(
    TextSpan identifier,
    IReadOnlyList<Import> imports,
    IReadOnlyList<ModuleItem> items
) {
    public TextSpan Identifier { get; } = identifier;
    public IReadOnlyList<Import> Imports { get; } = imports;
    public IReadOnlyList<ModuleItem> Items { get; } = items;


    public IEnumerable<string> Dependencies =>  Imports.Select(x => x.fromModule.ToString());


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

    // EXPORTS is not allowed in SMIv2 (section 3.3 of RFC 2578)
    // but some real-world MIBs include it, so we skip it if present.
    // https://www.rfc-editor.org/rfc/rfc2578.html#section-3.3
    //
    // Exports = "EXPORTS" <raw> ";"
    static readonly Parser<TextSpan> ExportsParser =
        Terms.Keyword("EXPORTS")
            .SkipAnd(AnyCharBefore(Terms.Char(';'), consumeDelimiter: true));

    // Module     = IDENT "DEFINITIONS" "::=" "BEGIN" ModuleBody? "END"
    // ModuleBody =  Exports? Imports? ModuleItem*
    public static Parser<ModuleDefinition> Module =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("DEFINITIONS").ElseError("Expected 'DEFINITIONS' after module name"))
            .AndSkip(Terms.Text("::=").ElseError("Expected '::=' after DEFINITIONS"))
            .AndSkip(Terms.Keyword("BEGIN").ElseError("Expected 'BEGIN'"))
            .AndSkip(ExportsParser.ZeroOrOne())
            .And(ImportsParser.Optional().Then(static x => x.OrSome([])))
            .And(ModuleItem.Parser.ZeroOrMany())
            .AndSkip(Terms.Keyword("END").ElseError("Expected 'END' or a valid top-level definition"))
            .Then(static x => new ModuleDefinition(x.Item1, x.Item2, x.Item3))
            .WithName("ModuleDefinition");

    public static Parser<ModuleDefinition> EntryPoint = Module;
};



public abstract class ModuleItem
{
    // ModuleItem = EntryDef
    //            | TextualConvention
    //            // Compliance, ignored
    //            | ObjectGroup
    //            | NotificationGroup
    //            | ModuleCompliance
    //            | AgentCapabilities
    //            | TrapType
    //            // Assignments 
    //            | ObjectIdentifier | ObjectIdentity
    //            | ModuleIdentity
    //            | NotificationType
    //            | ConceptualTable
    //            | ConceptualRow | AugmentingConceptualRow
    //            | LeafObject
    public static readonly Parser<ModuleItem> Parser =
        OneOf<ModuleItem>(
            EntryDef.Parser,
            TextualConvention.Parser,
            MacroObjectGroup.InnerParser,
            MacroNotificationGroup.InnerParser,
            MacroModuleCompliance.InnerParser,
            MacroAgentCapabilities.InnerParser,
            MacroTrapType.InnerParser,
            ObjectIdentifier.InnerParser,
            ObjectIdentifier.ObjectIdentity,
            ModuleIdentity.InnerParser,
            NotificationType.InnerParser,
            ConceptualTable.InnerParser,
            AugmentingConceptualRow.InnerParser,
            ConceptualRow.InnerParser,
            LeafObject.InnerParser
        ).WithName("ModuleItem");
}

public abstract class OidAssigner(
    TextSpan name,
    UnresolvedOid oid
) : ModuleItem {
    public TextSpan Name { get; } = name;
    public UnresolvedOid Oid { get; } = oid;
}

// Common ancestor for all OBJECT-TYPE based definitions
public abstract class ObjectType(
    TextSpan name,
    UnresolvedOid oid,
    SMIv2Accessibility accessibility,
    SMIv2Status status,
    TextSpan? description
) : OidAssigner(name, oid) {
    public SMIv2Accessibility Accessibility { get; } = accessibility;
    public SMIv2Status Status { get; } = status;
    public TextSpan? Description { get; } = description;
}

// A <EntryType> defintion in 7.1.12 "Conceptual Tables" of RFC2578
// https://www.rfc-editor.org/rfc/rfc2578.html#section-7.1.12
// OBJECT-TYPE with SYNTAX SEQUENCE OF <EntryType>
public class EntryDef(
    TextSpan name,
    IReadOnlyList<(TextSpan name, AstType type)> fields
) : ModuleItem {
    public TextSpan Name { get; } = name;
    public IReadOnlyList<(TextSpan name, AstType type)> Fields { get; } = fields;

    //  EntryDef = IDENT "::=" "SEQUENCE" "{"
    //      (RowEntryItem ",")* RowEntryItem
    //  "}"
    //  RowEntryItem = IDENT Type
    new public static readonly Parser<EntryDef> Parser =
        SMIv2.Ident
            .AndSkip(Terms.Text("::="))
            .AndSkip(Terms.Keyword("SEQUENCE"))
            .And(SMIv2.Block(
                Separated(Terms.Char(','), SMIv2.Ident.And(AstType.Parser))
            ))
            .Then(static x => new EntryDef(x.Item1, x.Item2))
            .WithName("EntryDef");
}

// SNMP `TEXTUAL-CONVENTION`
// Basically a typedef with a display hint
// See https://www.rfc-editor.org/rfc/rfc2579.html#section-3
public class TextualConvention(
    TextSpan name,
    AstType syntax
) : ModuleItem {
    public TextSpan Name { get; } = name;
    public AstType Syntax { get; } = syntax;

    //  TextualConvention = IDENT "::=" "TEXTUAL-CONVENTION"
    //       ("DISPLAY-HINT" <string>)?
    //       STATUS <status>
    //       DESCRIPTION <string>
    //       (REFERENCE <string>)?
    //       SYNTAX <type>
    new public static readonly Parser<TextualConvention> Parser =
        SMIv2.Ident
            .AndSkip(Terms.Text("::="))
            .AndSkip(Terms.Keyword("TEXTUAL-CONVENTION"))
            .AndSkip(Terms.Keyword("DISPLAY-HINT").AndSkip(SMIv2.String).ZeroOrOne())
            .AndSkip(SMIv2.OTStatusPart)
            .AndSkip(SMIv2.OTDescriptionPart)
            .AndSkip(SMIv2.OTReferPart.ZeroOrOne())
            .And(SMIv2.OTSyntaxPart)
            .Then(static x => new TextualConvention(x.Item1, x.Item2))
            .WithName("TextualConvention");
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
            .Then(static x => new ObjectIdentifier(x.Item1, x.Item2))
            .WithName("ObjectIdentifier");

    // Seldom used variant with some extra metadata
    //
    // ObjectIdentity =
    //      IDENT "OBJECT-IDENTITY"
    //      OTStatusPart OTDescriptionPart OTReferPart?
    //      OidAssignment
    public static readonly Parser<ObjectIdentifier> ObjectIdentity =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-IDENTITY"))
            .AndSkip(SMIv2.OTStatusPart)
            .AndSkip(SMIv2.OTDescriptionPart)
            .AndSkip(SMIv2.OTReferPart.ZeroOrOne())
            .And(SMIv2.OidAssignment)
            .Then(static x => new ObjectIdentifier(x.Item1, x.Item2))
            .WithName("ObjectIdentity");
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
            .SkipAnd(SMIv2.String)
            .AndSkip(Terms.Keyword("DESCRIPTION"))
            .And(SMIv2.String);

    //  ModuleIdentity = "MODULE-IDENTITY"
    //      "LAST-UPDATED" <string>
    //      "ORGANIZATION" <string>
    //      "CONTACT-INFO" <string>
    //      "DESCRIPTION"  <string>
    //      Revision*
    //      OidAssignment
    public static readonly Parser<ModuleIdentity> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("MODULE-IDENTITY"))
            .AndSkip(Terms.Keyword("LAST-UPDATED").ElseError("Expected 'LAST-UPDATED' in MODULE-IDENTITY"))
            .And(SMIv2.String.ElseError("Expected date string after LAST-UPDATED"))
            .AndSkip(Terms.Keyword("ORGANIZATION").ElseError("Expected 'ORGANIZATION' in MODULE-IDENTITY"))
            .And(SMIv2.String.ElseError("Expected string after ORGANIZATION"))
            .AndSkip(Terms.Keyword("CONTACT-INFO").ElseError("Expected 'CONTACT-INFO' in MODULE-IDENTITY"))
            .And(SMIv2.String.ElseError("Expected string after CONTACT-INFO"))
            .AndSkip(Terms.Keyword("DESCRIPTION").ElseError("Expected 'DESCRIPTION' in MODULE-IDENTITY"))
            .And(SMIv2.String.ElseError("Expected string after DESCRIPTION"))
            .And(Revision.ZeroOrMany())
            .And(SMIv2.OidAssignment)
            .Then(static x => new ModuleIdentity(
                x.Item1, x.Item7,
                x.Item2, x.Item3, x.Item4, x.Item5, x.Item6))
            .WithName("ModuleIdentity");
}

// SNMP `NOTIFICATION-TYPE`
public class NotificationType(
    TextSpan name,
    UnresolvedOid oid,
    SMIv2Status status,
    IReadOnlyList<TextSpan> objects,
    TextSpan? description
) : OidAssigner(name, oid) {
    public SMIv2Status Status { get; } = status;
    public IReadOnlyList<TextSpan> Objects { get; } = objects;
    public TextSpan? Description { get; } = description;

    // ObjectsPart = "OBJECTS" "{" Objects "}" | empty
    static readonly Parser<IReadOnlyList<TextSpan>> ObjectsPart =
        Terms.Keyword("OBJECTS")
            .SkipAnd(SMIv2.Block(SMIv2.Idents));

    // NotificationType = IDENT "NOTIFICATION-TYPE"
    //      ObjectsPart?
    //      OTStatusPart
    //      OTDescriptionPart
    //      OTReferPart?
    //      OidAssignment
    public static readonly Parser<NotificationType> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("NOTIFICATION-TYPE"))
            .And(ObjectsPart.Optional().Then(static x => x.OrSome([])))
            .And(SMIv2.OTStatusPart)
            .And(SMIv2.OTDescriptionPart)
            .AndSkip(SMIv2.OTReferPart.ZeroOrOne())
            .And(SMIv2.OidAssignment)
            .Then(static x => new NotificationType(
                x.Item1, x.Item5, x.Item3, x.Item2, x.Item4))
            .WithName("NotificationType");
}

// A "Conceptual Table" in 7.1.12 "Conceptual Tables" of RFC2578
// https://www.rfc-editor.org/rfc/rfc2578.html#section-7.1.12
// OBJECT-TYPE with SYNTAX SEQUENCE OF <EntryType>
public class ConceptualTable(
    TextSpan name,
    UnresolvedOid oid,
    AstType entryType,
    SMIv2Accessibility accessibility,
    SMIv2Status status,
    TextSpan? description
) : ObjectType(name, oid, accessibility, status, description) {
    public AstType EntryType { get; } = entryType;

    //  ConceptualTable = IDENT "OBJECT-TYPE"
    //      "SYNTAX" "SEQUENCE" "OF" <type>
    //      OTMiddlePart
    //      OidAssignment
    public static readonly Parser<ConceptualTable> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-TYPE"))
            .AndSkip(Terms.Keyword("SYNTAX"))
            .AndSkip(Terms.Keyword("SEQUENCE"))
            .AndSkip(Terms.Keyword("OF"))
            .And(AstType.Parser.ElseError("Expected entry type after SEQUENCE OF"))
            .And(SMIv2.OTMiddlePart)
            .And(SMIv2.OidAssignment)
            .Then(static x => new ConceptualTable(
                x.Item1, x.Item4, x.Item2, x.Item3.Item1, x.Item3.Item2, x.Item3.Item3))
            .WithName("ConceptualTable");
}

// A "Conceptual Row" in 7.1.12 "Conceptual Tables" of RFC2578
// https://www.rfc-editor.org/rfc/rfc2578.html#section-7.1.12
// OBJECT-TYPE with SYNTAX <EntryType> and INDEX clause
public class ConceptualRow(
    TextSpan name,
    UnresolvedOid oid,
    AstType entryType,
    SMIv2Accessibility accessibility,
    SMIv2Status status,
    TextSpan? description,
    IReadOnlyList<(TextSpan Name, bool IsImplied)> index
) : ObjectType(name, oid, accessibility, status, description) {
    public AstType EntryType { get; } = entryType;
    public IReadOnlyList<(TextSpan Name, bool IsImplied)> Index { get; } = index;

    // See "7.7.  Mapping of the INDEX clause" of RFC2578
    // https://www.rfc-editor.org/rfc/rfc2578.html#section-7.7
    // IMPLIED means the index value is not length-prefixed in the OID encoding;
    // it can only appear on the last index component.
    //
    // OTIndexPart = "INDEX" "{" OTIndexItems "}"
    // OTIndexItems = (IDENT ",")* "IMPLIED" IDENT
    //              | (IDENT ",")* IDENT
    static readonly Parser<(TextSpan Name, bool IsImplied)> IndexItem =
        Terms.Keyword("IMPLIED").ZeroOrOne()
            .And(SMIv2.Ident)
            .Then(static x => (x.Item2, x.Item1 is not null));

    public static readonly Parser<IReadOnlyList<(TextSpan, bool)>> OTIndexPart =
        Terms.Keyword("INDEX").SkipAnd(SMIv2.Block(
            Separated(Terms.Char(','), IndexItem)));

    //  ConceptualRow = IDENT "OBJECT-TYPE"
    //      "SYNTAX" <ident>
    //      "MAX-ACCESS" not-accessible
    //      "STATUS" <ident>
    //      ("DESCRIPTION" <string>)?
    //      ("REFERENCE" <string>)?
    //      "INDEX" "{" ident, ... "}"
    //      OidAssignment
    public static readonly Parser<ConceptualRow> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-TYPE"))
            .And(SMIv2.OTSyntaxPart)
            .And(SMIv2.OTMiddlePart)
            .And(OTIndexPart)
            .And(SMIv2.OidAssignment)
            .Then(static x => new ConceptualRow(
                x.Item1, x.Item5, x.Item2, x.Item3.Item1, x.Item3.Item2, x.Item3.Item3, x.Item4))
            .WithName("ConceptualRow");
}

// A "Conceptual Row Augmentation" in 7.8 "Mapping of the AUGMENTS clause" of RFC2578
// https://www.rfc-editor.org/rfc/rfc2578.html#section-7.8
// OBJECT-TYPE with SYNTAX <EntryType> and AUGMENTS clause
public class AugmentingConceptualRow(
    TextSpan name,
    UnresolvedOid oid,
    AstType entryType,
    SMIv2Accessibility accessibility,
    SMIv2Status status,
    TextSpan? description,
    TextSpan augments
) : ObjectType(name, oid, accessibility, status, description) {
    public AstType EntryType { get; } = entryType;
    public TextSpan Augments { get; } = augments;

    // OTAugmentsPart = "AUGMENTS" "{" Entry "}"
    public static readonly Parser<TextSpan> OTAugmentsPart =
        Terms.Keyword("AUGMENTS").SkipAnd(SMIv2.Block(SMIv2.Ident));

    //  AugmentingConceptualRow = IDENT "OBJECT-TYPE"
    //      "SYNTAX" <ident>
    //      "MAX-ACCESS" not-accessible
    //      "STATUS" <ident>
    //      ("DESCRIPTION" <string>)?
    //      ("REFERENCE" <string>)?
    //      "AUGMENTS" "{" entryTypeName "}"
    //      OidAssignment
    public static readonly Parser<AugmentingConceptualRow> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-TYPE"))
            .And(SMIv2.OTSyntaxPart)
            .And(SMIv2.OTMiddlePart)
            .And(OTAugmentsPart)
            .And(SMIv2.OidAssignment)
            .Then(static x => new AugmentingConceptualRow(
                x.Item1, x.Item5, x.Item2, x.Item3.Item1, x.Item3.Item2, x.Item3.Item3, x.Item4))
            .WithName("AugmentingConceptualRow");
}

// OBJECT-TYPE for leafs
public class LeafObject(
    TextSpan name,
    UnresolvedOid oid,
    AstType syntax,
    SMIv2Accessibility accessibility,
    SMIv2Status status,
    TextSpan? description
) : ObjectType(name, oid, accessibility, status, description) {
    public AstType Syntax { get; } = syntax;

    // This is simply ignored
    // OTDefValPart = "DEFVAL" "{" "{" IDENT ("," IDENT)* "}""}"
    //              | "DEFVAL" "{" <raw> "}"
    public static readonly Parser<TextSpan> OTDefValPart =
        Terms.Keyword("DEFVAL").SkipAnd(SMIv2.Block(
            OneOf(
                Capture(SMIv2.Block(SMIv2.Idents.Optional())),
                AnyCharBefore(Terms.Char('}'))
            ).ElseError("Expected DEFVAL value")
        )).WithName("OTDefValPart");

    // LeafObject = IDENT "OBJECT-TYPE"
    //      OTSyntaxPart
    //      OTMiddlePart
    //      OTDefValPart?
    //      OidAssignment
    public static readonly Parser<LeafObject> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("OBJECT-TYPE"))
            .And(SMIv2.OTSyntaxPart)
            .And(SMIv2.OTMiddlePart)
            .AndSkip(OTDefValPart.ZeroOrOne())
            .And(SMIv2.OidAssignment)
            .Then(static x => new LeafObject(
                x.Item1, x.Item4, x.Item2, x.Item3.Item1, x.Item3.Item2, x.Item3.Item3))
            .WithName("LeafObject");
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
            .Then(static x => instance)
            .WithName("ObjectGroup");
}

// SNMP `NOTIFICATION-GROUP`
// Note: Since we don't care about compliance, we cheat when parsing this
public class MacroNotificationGroup : ModuleItem {
    private static readonly MacroNotificationGroup instance = new();

    //  NotificationGroup = IDENT "NOTIFICATION-GROUP"
    //      "NOTIFICATIONS" "{" <raw> "}"
    //      "STATUS" <status>
    //      "DESCRIPTION" <string>
    //      ("REFERENCE" <string>)?
    //      "::=" "{" <raw> "}"
    public static readonly Parser<MacroNotificationGroup> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("NOTIFICATION-GROUP"))
            .AndSkip(AnyCharBefore(Literals.Text("::="), consumeDelimiter: true))
            .AndSkip(SMIv2.Block(AnyCharBefore(Terms.Char('}'))))
            .Then(static x => instance)
            .WithName("NotificationGroup");
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
            .Then(static x => instance)
            .WithName("ModuleCompliance");
}

// SNMP `AGENT-CAPABILITIES` macro (RFC 2580)
// We don't use this for code generation, so we skip it entirely.
public class MacroAgentCapabilities : ModuleItem {
    private static readonly MacroAgentCapabilities instance = new();

    //  AgentCapabilities = IDENT "AGENT-CAPABILITIES"
    //      ... (complex body)
    //      "::=" "{" <raw> "}"
    public static readonly Parser<MacroAgentCapabilities> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("AGENT-CAPABILITIES"))
            .AndSkip(AnyCharBefore(Literals.Text("::="), consumeDelimiter: true))
            .AndSkip(SMIv2.Block(AnyCharBefore(Terms.Char('}'))))
            .Then(static x => instance)
            .WithName("AgentCapabilities");
}

// SNMPv1 `TRAP-TYPE` macro (RFC 1215)
// Legacy construct; we skip it but still need to consume it to avoid parse errors.
public class MacroTrapType : ModuleItem {
    private static readonly MacroTrapType instance = new();

    //  TrapType = IDENT "TRAP-TYPE"
    //      "ENTERPRISE" IDENT
    //      ("VARIABLES" "{" Idents "}")?
    //      ("DESCRIPTION" <string>)?
    //      ("REFERENCE" <string>)?
    //      "::=" <number>
    public static readonly Parser<MacroTrapType> InnerParser =
        SMIv2.Ident
            .AndSkip(Terms.Keyword("TRAP-TYPE"))
            .AndSkip(AnyCharBefore(Literals.Text("::="), consumeDelimiter: true))
            .AndSkip(Terms.Integer())
            .Then(static x => instance)
            .WithName("TrapType");
}
