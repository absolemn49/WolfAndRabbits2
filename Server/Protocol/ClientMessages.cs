namespace WolfAndRabbits.Server.Protocol;

public sealed class ClientEnvelope
{
    public string type { get; set; } = string.Empty;
    public string? requestId { get; set; }
    public string? playerName { get; set; }
    public string? password { get; set; }
    public string? roomCode { get; set; }
    public float dirX { get; set; }
    public float dirY { get; set; }
    public string? buildingType { get; set; }
    public int tileX { get; set; }
    public int tileY { get; set; }
    public int buildingId { get; set; }
    public string? itemId { get; set; }
}
