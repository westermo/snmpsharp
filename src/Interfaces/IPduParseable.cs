namespace SnmpSharpNet;

public interface IPduParseable<out TSelf>
{
    public static abstract TSelf? Parse(Pdu pdu);
}
