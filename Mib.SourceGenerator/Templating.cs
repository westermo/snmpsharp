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

    public static string[] TableEntry(string accessibility, MibTable table, string ns)
    {
        var typeName = table.EntryType();
        var strip = typeName.Replace("TableEntry", string.Empty);
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
                .. DocComment($"Index #{i + 1}", idx.Leaf.Description),
                $"public required {idx.Type.AsUintType()} Index{idx.Ident.CSharpName(strip)};",
            ]).Indent(),
            "",
            .. table.Columns.SelectMany((col, i) => (string[])
            [
                "",
                .. DocComment($"Column #{i + 1}", col.Description),
                $"public required {col.Type.AsAsnType()} {col.Ident.CSharpName(strip)};",
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
        var strip = typeName.Replace("TableEntry", string.Empty);
        var indexPart = table.Index.SelectMany((index, _) =>
        {
            if (index.IsImplied && index.Type.UintCount is null)
            {
                // IMPLIED: no length prefix, consume the rest of the index span
                return (string[])
                [
                    $"var index{index.Ident.CSharpName(strip)} = ({index.Type.AsUintType()})index.ToArray();",
                    "index = default;"
                ];
            }

            return index.Type.UintCount switch
            {
                null =>
                [
                    $"var index{index.Ident.CSharpName(strip)} = ({index.Type.AsUintType()})index[1..(int)(1+index[0])].ToArray();",
                    "index = index[(int)(1+index[0])..];"
                ],
                1 => [$"var index{index.Ident.CSharpName(strip)} = index[0];", "index = index[1..];"],
                _ => (string[])
                [
                    $"var index{index.Ident.CSharpName(strip)} = ({index.Type.AsUintType()})index[..(int){index.Type.UintCount}].ToArray();",
                    $"index = index[(int){index.Type.UintCount}..];"
                ]
            };
        });


        return
        [
            $"public static {typeName}? Parse(ReadOnlySpan<uint> index, IReadOnlyDictionary<uint, AsnType> values)",
            "{",
            .. indexPart.Indent(),
            "",
            .. table.Columns.SelectMany((col, i) =>
            {
                var colName = col.Ident.CSharpName(strip);
                return new[]
                {
                    $"if(!values.TryGetValue({col.Ident.Oid.Last()}, out var _{colName}) || _{colName} is not {col.Type.AsAsnType()} {colName}) return null;",
                };
            }).Indent(),
            "",
            $"\treturn new {typeName}()",
            "\t{",
            .. table.Index.Select(x =>
                $"\t\tIndex{x.Ident.CSharpName(strip)} = index{x.Ident.CSharpName(strip)},"),
            .. table.Columns.Select(x =>
                $"\t\t{x.Ident.CSharpName(strip)} = {x.Ident.CSharpName(strip)},"),
            "\t};",
            "}"
        ];
    }

    public static string[] Populate(MibTable table)
    {
        var typeName = table.EntryType();
        var strip = typeName.Replace("TableEntry", string.Empty);
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
            .. table.Columns.Select(col =>
                $"\tresult[new Oid((uint[])[..Oid, 1, {col.Ident.Oid.Last()}, ..indexOid])] = {col.Ident.CSharpName(strip)};"
            ),
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