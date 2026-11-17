using System;
using System.Net;

namespace SnmpSharpNet;

/// <summary>
///     Community based SNMP target. Used for SNMP version 1 and version 2c.
/// </summary>
[Obsolete("Use UdpTarget for synchronous or asynchronous SNMP requests.")]
public class CTarget : ITarget
{
    #region Properties

    /// <summary>
    ///     SNMP community name for the target
    /// </summary>
    public string Community
    {
        get => _community;
        set => _community = value;
    }

    #endregion Properties

    #region Private variables

    /// <summary>
    ///     Target IP address
    /// </summary>
    protected readonly IpAddress _address;

    /// <summary>
    ///     Target port number
    /// </summary>
    protected int _port;

    /// <summary>
    ///     Target SNMP version number
    /// </summary>
    protected SnmpVersion _version;

    /// <summary>
    ///     Target request timeout period in milliseconds
    /// </summary>
    protected int _timeout;

    /// <summary>
    ///     Target maximum retry count
    /// </summary>
    protected int _retry;

    /// <summary>
    ///     SNMP community name
    /// </summary>
    protected string _community;

    #endregion

    #region Constructors

    /// <summary>
    ///     Constructor
    /// </summary>
    public CTarget()
    {
        _address = new IpAddress(IPAddress.Loopback);
        _port = 161;
        _version = SnmpVersion.Ver2;
        _timeout = 2000;
        _retry = 1;
        _community = "public";
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="address">Target address</param>
    public CTarget(IPAddress address)
        : this()
    {
        _address.Set(address);
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="address">Target address</param>
    /// <param name="community">SNMP community name to use with the target</param>
    public CTarget(IPAddress address, string community)
        : this(address)
    {
        _community = community;
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="address">Target address</param>
    /// <param name="port">Taret UDP port number</param>
    /// <param name="community">SNMP community name to use with the target</param>
    public CTarget(IPAddress address, int port, string community)
        : this(address, community)
    {
        _port = port;
    }

    #endregion Constructors

    #region ITarget Members

    /// <summary>
    ///     Prepare packet for transmission by filling target specific information in the packet.
    /// </summary>
    /// <param name="packet">SNMP packet class for the required version</param>
    /// <returns>True if packet values are correctly set, otherwise false.</returns>
    public bool PreparePacketForTransmission(SnmpPacket packet)
    {
        if (packet.Version != _version)
            return false;
        switch (_version)
        {
            case SnmpVersion.Ver1:
            {
                var pkt = (SnmpV1Packet)packet;
                pkt.Community.Set(_community);
                return true;
            }
            case SnmpVersion.Ver2:
            {
                var pkt = (SnmpV2Packet)packet;
                pkt.Community.Set(_community);
                return true;
            }
            case SnmpVersion.Ver3:
            default:
                return false;
        }
    }

    /// <summary>
    ///     Validate received reply
    /// </summary>
    /// <param name="packet">Received SNMP packet</param>
    /// <returns>True if packet is validated, otherwise false</returns>
    public bool ValidateReceivedPacket(SnmpPacket packet)
    {
        if (packet.Version != _version)
            return false;
        switch (_version)
        {
            case SnmpVersion.Ver1:
            {
                var pkt = (SnmpV1Packet)packet;
                if (pkt.Community.Equals(_community))
                    return true;
                break;
            }
            case SnmpVersion.Ver2:
            {
                var pkt = (SnmpV2Packet)packet;
                if (pkt.Community.Equals(_community))
                    return true;
                break;
            }
            case SnmpVersion.Ver3:
            default:
                break;
        }

        return false;
    }

    /// <summary>
    ///     Get version of SNMP protocol this target supports
    /// </summary>
    /// <exception cref="SnmpInvalidVersionException">Thrown when SNMP version other then 1 or 2c is set</exception>
    public SnmpVersion Version
    {
        get => _version;
        set
        {
            if (value != SnmpVersion.Ver1 && value != SnmpVersion.Ver2)
                throw new SnmpInvalidVersionException(
                    "CTarget is only suitable for use with SNMP v1 and v2c protocol versions.");
            _version = value;
        }
    }

    /// <summary>
    ///     Timeout in milliseconds for the target. Valid timeout values are between 100 and 10000 milliseconds.
    /// </summary>
    public int Timeout
    {
        get => _timeout;
        set
        {
            switch (value)
            {
                case < 100 or > 10000:
                    throw new OverflowException("Valid timeout value is between 100 milliseconds and 10000 milliseconds");
                default:
                    _timeout = value;
                    break;
            }
        }
    }

    /// <summary>
    ///     Number of retries for the target. Valid values are 0-5.
    /// </summary>
    public int Retry
    {
        get => _retry;
        set
        {
            switch (value)
            {
                case < 0 or > 5:
                    throw new OverflowException("Valid retry value is between 0 and 5");
                default:
                    _retry = value;
                    break;
            }
        }
    }

    /// <summary>
    ///     Target IP address
    /// </summary>
    public IpAddress Address => _address;

    /// <summary>
    ///     Target port number
    /// </summary>
    public int Port
    {
        get => _port;
        set => _port = value;
    }

    /// <summary>
    ///     Check validity of the target information.
    /// </summary>
    /// <returns>True if valid, otherwise false.</returns>
    public bool Valid()
    {
        switch (_community.Length)
        {
            case 0:
                return false;
        }
        switch (_address.Valid)
        {
            case false:
                return false;
            default:
                return _port != 0;
        }
    }

    #endregion
}