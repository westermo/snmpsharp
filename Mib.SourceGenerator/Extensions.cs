using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

using PartialClass = (string Accessibility, string? Namespace, string TypeName, Location Location);

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
            return $"{table.Ident.Name}Entry";
        }
        public string TableType()
        {
            return $"{table.EntryType()}[]";
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
                null => "uint[]",
                1 => "uint",
                _ => $"uint[{type.UintCount}]"
            };
        }
    }

    extension(PartialClass self)
    {
        public static PartialClass FromSymbol(ISymbol symbol)
        {
            var accessibility = symbol.DeclaredAccessibility switch {
                Accessibility.Public => "public",
                Accessibility.Private => "private",
                Accessibility.Internal => "internal",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => "private"
            };

            var ns =
                symbol.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : symbol.ContainingNamespace.ToDisplayString();
            return (accessibility, ns, symbol.Name, symbol.Locations.First());
        }
        public string[] AsLiteral(string impls, IEnumerable<string> body)
        {
            string[] decl = [
                $"{self.Accessibility} partial class {self.TypeName}{impls}",
                $"{{",
                ..body.Select(x => $"\t{x}"),
                $"}}",
            ];
            if (self.Namespace is {})
            {
                return [
                    $"namespace {self.Namespace}",
                    $"{{",
                    ..decl.Select(x => $"\t{x}"),
                    $"}}"
                ];
            }
            else
            {
                return decl;
            }
        }
    }

    extension(IEnumerable<string> lines)
    {
        public IEnumerable<string> Indent()
        {
            return lines.Select(line => $"\t{line}");
        }
    }
}
