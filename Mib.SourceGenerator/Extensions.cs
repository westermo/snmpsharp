using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Parlot;
using CodeSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace SnmpSharpNet.Mib.SourceGenerator;

internal static class Extensions
{
    extension(MibItemIdent ident)
    {
        public string AsLiteral()
        {
            return $"new Oid([{string.Join(", ", ident.Oid)}])";
        }
    }

    extension(MibTable table)
    {
        public string EntryType()
        {
            return $"{table.Ident.CSharpName(null)}Entry";
        }

        public string CommonPrefix()
        {
            return table.Index.Select(x => x.Ident.CSharpName(null))
                .Concat(table.Columns.Select(s => s.Ident.CSharpName(null)))
                .Append(table.Ident.CSharpName(null))
                .CommonPrefix();
        }
    }

    extension(MibItemIdent ident)
    {
        public string CSharpName(string? trimStart)
        {
            var baseName = OidTreeNaming.FormatIdentifier(ident.Name ?? "Unnamed");
            if (trimStart is null || string.IsNullOrEmpty(trimStart) || trimStart == baseName ||
                !baseName.StartsWith(trimStart) ||
                trimStart.Length > baseName.Length) return baseName;

            var trimmed = baseName.Substring(trimStart.Length, baseName.Length - trimStart.Length);
            return char.IsDigit(trimmed[0]) ? baseName : trimmed;
        }
    }

    extension(MibType type)
    {
        public string AsAsnType()
        {
            return type.Kind switch
            {
                TypeKind.Integer32Enum => "Integer32",
                TypeKind.Integer32 => "Integer32",
                TypeKind.Unsigned32 => "UInteger32",
                TypeKind.OctetString => "OctetString",
                TypeKind.IpAddress => "IpAddress",
                TypeKind.ObjectIdentifier => "Oid",
                TypeKind.Opaque => "Opaque",
                TypeKind.Bits => "OctetString",
                TypeKind.Counter32 => "Counter32",
                TypeKind.Counter64 => "Counter64",
                TypeKind.Gauge32 => "Gauge32",
                TypeKind.TimeTicks => "TimeTicks",
                _ => "AsnType"
            };
        }

        public string AsUintType()
        {
            return type.UintCount switch
            {
                1 => "uint",
                _ => "uint[]"
            };
        }
    }

    extension(IEnumerable<string> lines)
    {
        public IEnumerable<string> Indent()
        {
            return lines.Select(line => $"\t{line}");
        }

        public string CommonPrefix()
        {
            var array = lines.ToArray();

            if (array.Length == 0)
                return string.Empty;

            var count = 0;
            var first = array[0];
            if (first is null) return string.Empty;
            while (array.All(line => line is not null && line.Length > count && line[count] == first[count]))
            {
                count++;
            }

            return first.Substring(0, count);
        }
    }

    extension(string str1)
    {
        public string CommonPrefix(string str2)
        {
            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2)) return string.Empty;
            var count = 0;
            for (int i = 0; i < str1.Length && i < str2.Length; i++)
            {
                if (str1[i] == str2[i])
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            return str1.Substring(0, count);
        }
    }

    extension(Location location)
    {
        public static Location FromAdditionalText(AdditionalText text)
        {
            return Location.Create(text.Path, new CodeSpan(), new LinePositionSpan());
        }

        public Location WithPosition(TextPosition position)
        {
            var linePos = new LinePosition(position.Line, position.Column);
            return Location.Create(
                location.GetLineSpan().Path,
                new CodeSpan(position.Offset, 1),
                new LinePositionSpan(linePos, linePos)
            );
        }
    }


    extension(IReadOnlyDictionary<string, GenMibDefinition> asts)
    {
        public Result<Dictionary<string, MibModule>> ConstructModules()
        {
            try
            {
                return MibParser.ParseModules(asts.ToDictionary(x => x.Key, x => x.Value.Module.Value));
            }
            catch (MibConstructionException e)
            {
                return Diagnostic.Create(Diagnostics.ParseError, asts[e.ModuleName].Location, e.ToString());
            }
            catch (InvalidOperationException e)
            {
                return Diagnostic.Create(Diagnostics.MibNotFound, Location.None, e.Message);
            }
            catch (KeyNotFoundException e)
            {
                return Diagnostic.Create(Diagnostics.MibNotFound, Location.None, e.Message);
            }
        }
    }
}