
namespace SnmpSharpNet.Mib.SourceGenerator;

static class Extensions
{
    extension(MibItemIdent ident)
    {
        public string AsOidLiteral()
        {
            return $"new Oid([{string.Join(", ", ident.Oid)}])";
        }
    }
}