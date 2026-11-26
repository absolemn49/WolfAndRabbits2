using System.Collections.Concurrent;
using WolfAndRabbits.Server.Protocol;

namespace WolfAndRabbits.Server.Game;

public sealed class RoomManager
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _rng = new();
    private readonly object _codeLock = new();

    public List<RoomSummaryDto> GetJoinableRooms()
    {
        var list = new List<RoomSummaryDto>();
        foreach (var kv in _rooms)
        {
            var summary = kv.Value.GetLobbySummary();
            if (summary != null)
                list.Add(summary);
        }
        list.Sort((a, b) => string.Compare(a.roomCode, b.roomCode, StringComparison.Ordinal));
        return list;
    }

    public Room? GetRoom(string? roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return null;
        return _rooms.TryGetValue(roomCode.Trim().ToUpperInvariant(), out var room) ? room : null;
    }

    public Room CreateRoom(ClientConnection host, string playerName, string? password)
    {
        LeaveCurrentRoom(host);

        string cleanName = SanitizeName(playerName);
        host.PlayerName = cleanName;
        string cleanPassword = (password ?? string.Empty).Trim();

        string code = GenerateUniqueCode();
        var room = new Room(code, cleanPassword, host, cleanName);
        _rooms[code] = room;
        room.SendCurrentRoomState(host);
        return room;
    }

    public ActionResult JoinRoom(ClientConnection client, string? roomCode, string playerName, string? password)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            return ActionResult.Fail("invalid_code", "Vui lòng nhập mã phòng.");

        string key = roomCode.Trim().ToUpperInvariant();
        if (!_rooms.TryGetValue(key, out var room))
            return ActionResult.Fail("room_not_found", $"Không tìm thấy phòng {key}.");

        string cleanName = SanitizeName(playerName);
        client.PlayerName = cleanName;

        if (!string.Equals(client.CurrentRoomCode, key, StringComparison.OrdinalIgnoreCase))
            LeaveCurrentRoom(client);

        return room.TryAddMember(client, cleanName, password);
    }

    public void LeaveCurrentRoom(ClientConnection client)
    {
        string? code = client.CurrentRoomCode;
        if (string.IsNullOrEmpty(code)) return;

        if (_rooms.TryGetValue(code, out var room))
        {
            bool empty = room.RemoveMember(client.ClientId);
            if (empty)
            {
                _rooms.TryRemove(code, out _);
            }
        }
        client.CurrentRoomCode = null;
    }

    private string GenerateUniqueCode()
    {
        lock (_codeLock)
        {
            Span<char> buf = stackalloc char[4];
            for (int attempt = 0; attempt < 100; attempt++)
            {
                for (int i = 0; i < buf.Length; i++)
                    buf[i] = CodeAlphabet[_rng.Next(CodeAlphabet.Length)];
                string candidate = new string(buf);
                if (!_rooms.ContainsKey(candidate))
                    return candidate;
            }
            return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        }
    }

    private static string SanitizeName(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "Người chơi";
        return trimmed.Length > 20 ? trimmed[..20] : trimmed;
    }
}
