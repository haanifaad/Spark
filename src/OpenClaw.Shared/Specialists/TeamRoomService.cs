using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Specialists;

/// <summary>
/// A message posted inside a multi-agent team collaboration room.
/// </summary>
public sealed record TeamRoomMessage
{
    public required string MessageId { get; init; }
    public required string RoomId { get; init; }
    public required string AuthorId { get; init; } // "user" or agent identity e.g. "email_agent"
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> Mentions { get; init; } = Array.Empty<string>();
    public int ChainDepth { get; init; }
}

/// <summary>
/// A persistent collaborative room where users and multiple specialist agents collaborate.
/// </summary>
public sealed class TeamRoom
{
    public required string RoomId { get; init; }
    public required string Name { get; init; }
    public string Topic { get; set; } = "";
    public HashSet<string> Members { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TeamRoomMessage> Messages { get; } = new();
    public SemaphoreSlim Lock { get; } = new(1, 1);
}

/// <summary>
/// Multi-agent team room service for SPARK.
/// Facilitates structured collaboration between specialist agents, parsing @mentions,
/// enforcing origin fencing, and preventing recursive delegation storms.
/// </summary>
public sealed class TeamRoomService
{
    public const int MaxDelegationChainDepth = 5;
    private static readonly Regex MentionRegex = new(@"@([a-zA-Z0-9_\-]+)", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, TeamRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public TeamRoom CreateRoom(string name, string topic, IEnumerable<string> initialMembers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var room = new TeamRoom
        {
            RoomId = $"room_{Guid.NewGuid():N}",
            Name = name.Trim(),
            Topic = topic ?? ""
        };

        if (initialMembers != null)
        {
            foreach (var member in initialMembers)
            {
                if (!string.IsNullOrWhiteSpace(member))
                    room.Members.Add(member.Trim());
            }
        }

        _rooms[room.RoomId] = room;
        return room;
    }

    public TeamRoom? GetRoom(string roomId) =>
        _rooms.TryGetValue(roomId, out var room) ? room : null;

    public IReadOnlyList<TeamRoom> GetAllRooms() => _rooms.Values.ToList();

    /// <summary>
    /// Posts a message to a team room, parsing @mentions and checking recursion limits.
    /// </summary>
    public async Task<TeamRoomMessage> PostMessageAsync(
        string roomId,
        string authorId,
        string content,
        int currentDepth = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorId);
        content ??= "";

        if (!_rooms.TryGetValue(roomId, out var room))
            throw new KeyNotFoundException($"Team room '{roomId}' was not found.");

        if (currentDepth > MaxDelegationChainDepth)
        {
            throw new InvalidOperationException(
                $"Delegation depth limit of {MaxDelegationChainDepth} exceeded. Circular delegation terminated.");
        }

        var mentions = ParseMentions(content);

        await room.Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var msg = new TeamRoomMessage
            {
                MessageId = $"msg_{Guid.NewGuid():N}",
                RoomId = roomId,
                AuthorId = authorId,
                Content = content,
                Mentions = mentions,
                ChainDepth = currentDepth,
                Timestamp = DateTimeOffset.UtcNow
            };

            room.Messages.Add(msg);
            return msg;
        }
        finally
        {
            room.Lock.Release();
        }
    }

    public static IReadOnlyList<string> ParseMentions(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<string>();

        var matches = MentionRegex.Matches(content);
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                list.Add(match.Groups[1].Value);
            }
        }

        return list.ToList();
    }
}
