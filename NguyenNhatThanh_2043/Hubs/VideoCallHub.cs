using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NguyenNhatThanh_2043.Data;
using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Hubs;

public class ParticipantSession
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsCameraOff { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

[Authorize]
public class VideoCallHub : Hub
{
    // =========================================================================
    // LƯU Ý 1: Quản lý Ánh xạ ConnectionId Bằng Bộ Nhớ Tạm Nhanh (In-Memory Traversal)
    // Tra cứu và chuyển tiếp tín hiệu SDP/ICE Candidate tức thời, 0ms I/O DB
    // =========================================================================
    private static readonly ConcurrentDictionary<string, string> _userToConnectionMap = new();
    private static readonly ConcurrentDictionary<string, string> _connectionToUserMap = new();
    private static readonly ConcurrentDictionary<string, string> _connectionToRoomMap = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ParticipantSession>> _roomParticipants = new();

    private readonly AppDbContext _context;
    private readonly ILogger<VideoCallHub> _logger;

    public VideoCallHub(AppDbContext context, ILogger<VideoCallHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Tham gia phòng gọi video nhóm
    /// </summary>
    public async Task JoinRoom(string roomCode)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = Context.User?.Identity?.Name ?? "User";
        var fullName = Context.User?.FindFirstValue("FullName") ?? username;

        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Không thể xác thực danh tính người dùng.");
        }

        // Kiểm tra tính hợp lệ của phòng trong DB
        var room = await _context.CallRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        if (room == null)
        {
            throw new HubException($"Không tìm thấy phòng gọi với mã '{roomCode}'.");
        }

        if (room.Status == "Ended")
        {
            throw new HubException("Phòng gọi này đã kết thúc.");
        }

        var groupName = $"call_room_{roomCode}";

        // 1. Ánh xạ bộ nhớ tạm (In-Memory Traversal)
        _userToConnectionMap[userId] = Context.ConnectionId;
        _connectionToUserMap[Context.ConnectionId] = userId;
        _connectionToRoomMap[Context.ConnectionId] = roomCode;

        var roomDict = _roomParticipants.GetOrAdd(roomCode, _ => new ConcurrentDictionary<string, ParticipantSession>());

        if (roomDict.Count >= room.MaxParticipants)
        {
            throw new HubException($"Phòng gọi đã đạt giới hạn tối đa ({room.MaxParticipants} người).");
        }

        var session = new ParticipantSession
        {
            ConnectionId = Context.ConnectionId,
            UserId = userId,
            Username = username,
            FullName = fullName,
            IsMuted = false,
            IsCameraOff = false,
            JoinedAt = DateTime.UtcNow
        };
        roomDict[Context.ConnectionId] = session;

        // 2. Thêm vào SignalR Group
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // 3. Trả về danh sách người tham gia hiện tại cho Caller
        var existingParticipants = roomDict.Values
            .Where(p => p.ConnectionId != Context.ConnectionId)
            .ToList();
        await Clients.Caller.SendAsync("ExistingParticipants", existingParticipants);

        // 4. Thông báo cho các thành viên khác trong phòng
        await Clients.OthersInGroup(groupName).SendAsync("UserJoined", session);

        _logger.LogInformation("[VIDEOCALL JOIN] '{Username}' ({UserId}) đã tham gia phòng '{RoomCode}' (Conn: {ConnectionId})",
            username, userId, roomCode, Context.ConnectionId);

        // 5. Ghi nhận vào DB trong Background Task để không làm chậm WebSocket
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = Context.GetHttpContext()?.RequestServices.CreateScope();
                if (scope != null)
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var participant = new CallParticipant
                    {
                        RoomId = room.Id,
                        UserId = userId,
                        ConnectionId = Context.ConnectionId,
                        JoinedAt = DateTime.UtcNow,
                        IsMuted = false,
                        IsCameraOff = false
                    };
                    db.CallParticipants.Add(participant);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu người tham gia phòng vào DB");
            }
        });
    }

    /// <summary>
    /// Rời phòng gọi video
    /// </summary>
    public async Task LeaveRoom(string roomCode)
    {
        await HandleUserLeaveAsync(Context.ConnectionId, roomCode);
    }

    /// <summary>
    /// LƯU Ý 1: Chuyển tiếp tín hiệu WebRTC (SDP Offer, SDP Answer, ICE Candidate)
    /// Tra cứu tức thời qua targetConnectionId trong bộ nhớ tạm - Không gọi DB!
    /// </summary>
    public async Task SendSignal(string targetConnectionId, object signalData)
    {
        if (string.IsNullOrEmpty(targetConnectionId)) return;

        // Chuyển tiếp trực tiếp tới target client
        await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", Context.ConnectionId, signalData);
    }

    /// <summary>
    /// Bật/Tắt Mic hoặc Camera
    /// </summary>
    public async Task ToggleMedia(string roomCode, bool isMuted, bool isCameraOff)
    {
        var groupName = $"call_room_{roomCode}";
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (_roomParticipants.TryGetValue(roomCode, out var roomDict) &&
            roomDict.TryGetValue(Context.ConnectionId, out var session))
        {
            session.IsMuted = isMuted;
            session.IsCameraOff = isCameraOff;
        }

        await Clients.OthersInGroup(groupName).SendAsync("MediaStateChanged", new
        {
            connectionId = Context.ConnectionId,
            userId,
            isMuted,
            isCameraOff
        });
    }

    /// <summary>
    /// =========================================================================
    /// LƯU Ý 2: Phân Quyền Kết Thúc Phòng Gọi Chặt Chẽ
    /// Chỉ người tạo phòng (HostUserId) hoặc tài khoản Role Admin được quyền đóng phòng.
    /// Broadcast CallEnded để tất cả client trong phòng tự động thoát ra ngoài.
    /// =========================================================================
    /// </summary>
    public async Task EndGroupCall(string roomCode)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Không thể xác thực danh tính người dùng.");
        }

        var room = await _context.CallRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        if (room == null)
        {
            throw new HubException($"Không tìm thấy phòng gọi '{roomCode}'.");
        }

        // Kiểm tra phân quyền: Phải là HostUserId hoặc có Role Admin
        var isAdmin = Context.User?.IsInRole("Admin") ?? false;
        var isHost = room.HostUserId == userId;

        if (!isHost && !isAdmin)
        {
            throw new HubException("Bạn không có quyền kết thúc phòng gọi này. Chỉ chủ phòng hoặc Admin mới được phép đóng phòng.");
        }

        // Cập nhật trạng thái phòng trong DB
        room.Status = "Ended";
        room.EndedAt = DateTime.UtcNow;

        // Cập nhật LeftAt cho tất cả thành viên chưa rời phòng
        var ongoingParticipants = await _context.CallParticipants
            .Where(p => p.RoomId == room.Id && p.LeftAt == null)
            .ToListAsync();
        foreach (var p in ongoingParticipants)
        {
            p.LeftAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        var groupName = $"call_room_{roomCode}";

        // Broadcast sự kiện CallEnded đến toàn bộ người trong phòng
        await Clients.Group(groupName).SendAsync("CallEnded", new
        {
            roomCode,
            message = "Cuộc gọi đã được kết thúc bởi chủ phòng/quản trị viên.",
            endedAt = room.EndedAt
        });

        // Xóa sạch bộ nhớ tạm của phòng
        if (_roomParticipants.TryRemove(roomCode, out var participants))
        {
            foreach (var connId in participants.Keys)
            {
                _connectionToRoomMap.TryRemove(connId, out _);
                if (_connectionToUserMap.TryRemove(connId, out var uId))
                {
                    _userToConnectionMap.TryRemove(uId, out _);
                }
            }
        }

        _logger.LogInformation("[VIDEOCALL ENDED] Phòng '{RoomCode}' đã được đóng bởi UserId: {UserId} (IsAdmin: {IsAdmin})",
            roomCode, userId, isAdmin);
    }

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name ?? "Anonymous";
        _logger.LogInformation("[SIGNALR VIDEOCALL CONNECT] '{Username}' đã kết nối VideoCallHub (ConnId: {ConnectionId})",
            username, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionToRoomMap.TryGetValue(Context.ConnectionId, out var roomCode))
        {
            await HandleUserLeaveAsync(Context.ConnectionId, roomCode);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task HandleUserLeaveAsync(string connectionId, string roomCode)
    {
        var groupName = $"call_room_{roomCode}";
        var userId = _connectionToUserMap.TryGetValue(connectionId, out var uId) ? uId : null;

        // 1. Dọn dẹp In-Memory Map
        _connectionToRoomMap.TryRemove(connectionId, out _);
        _connectionToUserMap.TryRemove(connectionId, out _);
        if (userId != null)
        {
            _userToConnectionMap.TryRemove(userId, out _);
        }

        if (_roomParticipants.TryGetValue(roomCode, out var roomDict))
        {
            roomDict.TryRemove(connectionId, out _);
            if (roomDict.IsEmpty)
            {
                _roomParticipants.TryRemove(roomCode, out _);
            }
        }

        // 2. Rời khỏi SignalR Group
        await Groups.RemoveFromGroupAsync(connectionId, groupName);

        // 3. Thông báo cho các thành viên còn lại
        await Clients.Group(groupName).SendAsync("UserLeft", new
        {
            connectionId,
            userId
        });

        _logger.LogInformation("[VIDEOCALL LEAVE] ConnectionId: {ConnectionId} đã rời phòng '{RoomCode}'",
            connectionId, roomCode);

        // 4. Cập nhật LeftAt trong DB
        if (userId != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var room = await _context.CallRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
                    if (room != null)
                    {
                        var participant = await _context.CallParticipants
                            .Where(p => p.RoomId == room.Id && p.UserId == userId && p.LeftAt == null)
                            .OrderByDescending(p => p.JoinedAt)
                            .FirstOrDefaultAsync();
                        if (participant != null)
                        {
                            participant.LeftAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi cập nhật thời gian rời phòng trong DB");
                }
            });
        }
    }
}
