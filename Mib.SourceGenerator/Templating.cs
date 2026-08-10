using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;

namespace SnmpSharpNet.Mib.SourceGenerator;

public static class Templating
{
    private static readonly AssemblyName AssemblyName = Assembly.GetExecutingAssembly().GetName();
    public static string Attribute => $"[GeneratedCode(\"{AssemblyName.Name}\",\"{AssemblyName.Version}\")]";

    public static string[] DocComment(params string?[] content)
    {
        var lines = content
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Cast<string>()
            .SelectMany(NormalizeDocLines)
            .Select(static line => line.Length == 0 ? "///" : $"/// {line}");

        return
        [
            "/// <summary>",
            .. lines,
            "/// </summary>"
        ];
    }

    public static string? DefaultFactory(MibLeaf leaf)
    {
        if (leaf.DefaultValue is not { } value)
        {
            return null;
        }

        var asnType = leaf.Type.AsAsnType();
        return (leaf.Type.Kind, value.Kind) switch
        {
            (TypeKind.Integer32 or TypeKind.Integer32Enum, MibDefaultValueKind.Number) =>
                $"new {asnType}({value.Number!.Value})",
            (TypeKind.Unsigned32 or TypeKind.Gauge32 or TypeKind.TimeTicks, MibDefaultValueKind.Number) =>
                $"new {asnType}({value.Number!.Value}u)",
            (TypeKind.OctetString or TypeKind.Opaque or TypeKind.IpAddress, MibDefaultValueKind.Octets) =>
                $"new {asnType}({ByteArrayLiteral(value.Octets)})",
            (TypeKind.ObjectIdentifier, MibDefaultValueKind.ObjectIdentifier) =>
                $"new Oid({UintArrayLiteral(value.ObjectIdentifier)})",
            (TypeKind.Bits, MibDefaultValueKind.Bits) =>
                $"new OctetString({ByteArrayLiteral(BitsToOctets(leaf.Type, value))})",
            _ => null
        };
    }

    private static IReadOnlyList<byte> BitsToOctets(MibType type, MibDefaultValue value)
    {
        IReadOnlyDictionary<string, long> bitValues =
            type.Values ?? new Dictionary<string, long>();
        var selectedBits = value.Names.Select(name => bitValues[name]).ToArray();
        if (selectedBits.Length == 0)
        {
            return [];
        }

        var result = new byte[selectedBits.Max(bit => (int)(bit / 8) + 1)];
        foreach (var bit in selectedBits)
        {
            result[(int)(bit / 8)] |= (byte)(1 << (int)(7 - bit % 8));
        }

        return result;
    }

    private static string ByteArrayLiteral(IEnumerable<byte> values) =>
        $"new byte[] {{ {string.Join(", ", values.Select(value => value.ToString()))} }}";

    private static string UintArrayLiteral(IEnumerable<uint> values) =>
        $"new uint[] {{ {string.Join(", ", values.Select(value => $"{value}u"))} }}";

    public static string[] TableEntry(string accessibility, MibTable table, string ns)
    {
        var typeName = table.EntryType();
        var strip = table.CommonPrefix();
        var oid = string.Join(".", table.Ident.Oid.Append(1u));
        return
        [
            "",
            .. DocComment(oid, table.EntryDescription),
            Attribute,
            $"{accessibility} class {typeName} : ISnmpTableEntry<{typeName}>",
            "{",
            $"\tpublic static Oid Oid => field ??= {table.Ident.AsLiteral()};",
            $"\tpublic static string BranchName => \"{typeName}\";",
            $"\tpublic static string FullBranchName => \"{ns}\";",
            .. table.Index.SelectMany((idx, i) => (string[])
            [
                "",
                .. DocComment($"Index #{i + 1}", idx.Ident.Name, idx.Leaf.Description, strip),
                $"public required {idx.Type.AsUintType()} Index{idx.Ident.CSharpName(strip)};",
            ]).Indent(),
            "",
            .. table.Columns.SelectMany((col, i) => (string[])
            [
                "",
                .. DocComment($"Column #{i + 1}", col.Description),
                $"public {col.Type.AsAsnType()}? {col.Ident.CSharpName(strip)};",
            ]).Indent(),
            "",
            .. IndexedParse(table).Indent(),
            "",
            .. Populate(table).Indent(),
            "}"
        ];
    }

    public static string[] IndexedParse(MibTable table)
    {
        var typeName = table.EntryType();
        var strip = table.CommonPrefix();
        var indexPart = IndexDecodeStatements(table, strip, "index", "return null;");

        return
        [
            $"public static {typeName}? Parse(ReadOnlySpan<uint> index, IReadOnlyDictionary<uint, AsnType> values)",
            "{",
            .. indexPart.Indent(),
            "\tif (!index.IsEmpty) return null;",
            "",
            $"\tvar entry = new {typeName}()",
            "\t{",
            .. table.Index.Select(x =>
                $"\t\tIndex{x.Ident.CSharpName(strip)} = index{x.Ident.CSharpName(strip)},"),
            "\t};",
            "",
            .. table.Columns.Select(col =>
            {
                var name = col.Ident.CSharpName(strip);
                var valueName = $"value{name}";
                return
                    $"if (values.TryGetValue({col.Ident.Oid.Last()}, out var _{name}) && _{name} is {col.Type.AsAsnType()} {valueName}) entry.{name} = {valueName};";
            }).Indent(),
            "",
            "\treturn entry;",
            "}"
        ];
    }

    public static IEnumerable<string> IndexDecodeStatements(
        MibTable table,
        string strip,
        string indexVariable,
        string invalidIndexStatement)
    {
        return table.Index.SelectMany((index, _) =>
        {
            var indexName = index.Ident.CSharpName(strip);
            if (index.IsImplied && index.Type.UintCount is null)
            {
                // IMPLIED: no length prefix, consume the rest of the index span
                return (string[])
                [
                    $"var index{indexName} = {indexVariable}.ToArray(); //Implied index encountered, consumes the rest",
                    $"{indexVariable} = default;"
                ];
            }

            return index.Type.UintCount switch
            {
                null =>
                [
                    $"if ({indexVariable}.Length == 0) {invalidIndexStatement}",
                    $"var {indexName}Length = {indexVariable}[0];",
                    $"if ({indexName}Length > {indexVariable}.Length - 1) {invalidIndexStatement}",
                    $"var index{indexName} = {indexVariable}[1..(int)(1 + {indexName}Length)].ToArray();",
                    $"{indexVariable} = {indexVariable}[(int)(1 + {indexName}Length)..];"
                ],
                1 =>
                [
                    $"if ({indexVariable}.Length < 1) {invalidIndexStatement}",
                    $"var index{indexName} = {indexVariable}[0];",
                    $"{indexVariable} = {indexVariable}[1..];"
                ],
                _ => (string[])
                [
                    $"if ({indexVariable}.Length < {index.Type.UintCount}) {invalidIndexStatement}",
                    $"var index{indexName} = {indexVariable}[..{index.Type.UintCount}].ToArray();",
                    $"{indexVariable} = {indexVariable}[(int){index.Type.UintCount}..];"
                ]
            };
        });
    }

    public static string[] Populate(MibTable table)
    {
        var strip = table.CommonPrefix();
        var indexParts = table.Index.SelectMany(index =>
        {
            if (index.IsImplied && index.Type.UintCount is null)
            {
                // IMPLIED: no length prefix
                return (string[])
                [
                    $"indexOid.AddRange(Index{index.Ident.CSharpName(strip)});"
                ];
            }

            return index.Type.UintCount switch
            {
                null =>
                [
                    $"indexOid.Add((uint)Index{index.Ident.CSharpName(strip)}.Length);",
                    $"indexOid.AddRange(Index{index.Ident.CSharpName(strip)});"
                ],
                1 => [$"indexOid.Add(Index{index.Ident.CSharpName(strip)});"],
                _ => (string[])[$"indexOid.AddRange(Index{index.Ident.CSharpName(strip)});"]
            };
        });

        return
        [
            "public void Populate(IDictionary<Oid, AsnType> result)",
            "{",
            "\tvar indexOid = new List<uint>();",
            .. indexParts.Indent(),
            .. table.Columns.SelectMany(col =>
            {
                var name = col.Ident.CSharpName(strip);
                var valueName = $"value{name}";
                return (string[])
                [
                    $"\tif ({name} is {{ }} {valueName})",
                    "\t{",
                    $"\t\tresult[new Oid((uint[])[..Oid, 1, {col.Ident.Oid.Last()}, ..indexOid])] = {valueName};",
                    "\t}"
                ];
            }),
            "}"
        ];
    }

    private static IEnumerable<string> NormalizeDocLines(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            yield break;
        }

        var indentation = lines
            .Skip(1)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(LeadingWhitespace)
            .DefaultIfEmpty(0)
            .Min();

        yield return EscapeDocLine(lines[0].Trim());

        foreach (var rawLine in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                yield return string.Empty;
                continue;
            }

            var line = rawLine.Length >= indentation
                ? rawLine.Substring(indentation)
                : rawLine.TrimStart();
            yield return EscapeDocLine(line.TrimEnd());
        }
    }

    private static int LeadingWhitespace(string value) =>
        value.TakeWhile(char.IsWhiteSpace).Count();

    private static string EscapeDocLine(string value) =>
        SecurityElement.Escape(value) ?? string.Empty;
}