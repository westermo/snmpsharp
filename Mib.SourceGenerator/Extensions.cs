
namespace SnmpSharpNet.Mib.SourceGenerator;

static class Extensions
{
    extension(MibItemIdent ident)
    {
        public string AsOidLiteral()
        {
            return $"new global::SnmpSharpNet.Oid([{string.Join(", ", ident.Oid)}])";
        }
    }
}
