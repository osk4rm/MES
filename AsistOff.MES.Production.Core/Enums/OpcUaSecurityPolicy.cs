namespace AsistOff.MES.Production.Domain.Enums;

/// <summary>
/// Security policy negotiated with the OPC UA server endpoint.
/// </summary>
public enum OpcUaSecurityPolicy : short
{
    None = 1,
    Basic256Sha256 = 2
}
