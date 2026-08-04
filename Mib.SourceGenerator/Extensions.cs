using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Parlot;

using CodeSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace SnmpSharpNet.Mib.SourceGenerator;

static class Extensions
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
            return $"{table.Ident.CSharpName()}Entry";
        }
        public string TableType()
        {
            return $"{table.EntryType()}[]";
        }
    }

    extension(MibItemIdent ident)
    {
        public string CSharpName() => OidTreeNaming.FormatIdentifier(ident.Name ?? "Unnamed");
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
