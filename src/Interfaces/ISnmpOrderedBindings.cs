namespace SnmpSharpNet;

public interface ISnmpOrderedBindings
{
    void Populate(VbCollection values);
}

public static class SnmpOrderedBindingsExtensions
{
    extension(ISnmpOrderedBindings bindings)
    {
        public VbCollection ToValueBindings()
        {
            var collection = new VbCollection();
            bindings.Populate(collection);
            return collection;
        }
    }

    extension<TSelf>(TSelf bindings) where TSelf : ISnmpNotification<TSelf>
    {
        public Pdu ToTrapPdu()
        {
            var pdu = new Pdu(PduType.V2Trap);
            bindings.Populate(pdu.VbList);
            pdu.TrapObjectID = bindings.InstanceOid;
            return pdu;
        }
    }
}