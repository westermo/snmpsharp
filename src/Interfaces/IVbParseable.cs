namespace SnmpSharpNet;

public interface IVbParseable<out TSelf>
{
    public static abstract TSelf? Parse(VbCollection values);
}
