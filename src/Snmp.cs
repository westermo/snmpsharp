using System;

namespace SnmpSharpNet;

[Obsolete("Use UdpTransport for datagram I/O.")]
public class Snmp : UdpTransport
{
    /// <summary>
    ///     Internal storage for request target information.
    /// </summary>
    protected ITarget? _target;

    #region Constructor(s)

    /// <summary>
    ///     Constructor
    /// </summary>
    public Snmp()
        : base(false)
    {
        _target = null;
    }

    #endregion Constructor(s)

}