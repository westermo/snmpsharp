using System.Collections.Generic;

namespace SnmpSharpNet;

public interface ISnmpBindings
{
    void Populate(IDictionary<Oid, AsnType> values);
}

public static class SnmpBindingExtensions
{
    extension(ISnmpBindings bindable)
    {
        public IDictionary<Oid, AsnType> ToDictionary()
        {
            var instance = new Dictionary<Oid, AsnType>();
            bindable.Populate(instance);
            return instance;
        }
    }
}