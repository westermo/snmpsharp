using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SnmpSharpNet.Mib.SourceGenerator;

using PartialClass = (string Accessibility, string? Namespace, string TypeName);

static class Extensions
{
    extension(MibItemIdent ident)
    {
        public string AsLiteral()
        {
            return $"new Oid([{string.Join(", ", ident.Oid)}])";
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
            return (accessibility, ns, symbol.Name);
        }
        public string[] AsLiteral(IEnumerable<string> body)
        {
            string[] decl = [
                $"{self.Accessibility} partial class {self.TypeName}",
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
}
