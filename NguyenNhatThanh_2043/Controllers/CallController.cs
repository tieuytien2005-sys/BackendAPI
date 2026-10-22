using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NguyenNhatThanh_2043.Data;
using NguyenNhatThanh_2043.DTOs;
using NguyenNhatThanh_2043.Hubs;
using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CallController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<VideoCallHub> _hubContext;
    private readonly ILogger<CallController> _logger;

    public CallController(
        AppDbContext context,
        IHubContext<VideoCallHub> hubContext,
        ILogger<CallController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// 1. Tạo phòng gọi video nhóm mới
    /// </summary>
    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateCallRoomDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Không xác thực được danh tính người dùng" });

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized(new { message = "Tài khoản không tồn tại" });

        // Sinh mã phòng ngẫu nhiên 6 ký tự số hoặc chuỗi ngắn dễ nhớ
        string roomCode;
        do
        {
            roomCode = Random.Shared.Next(100000, 999999).ToString();
        } while (await _context.CallRooms.AnyAsync(r => r.RoomCode == roomCode && r.Status == "Active"));

        var room = new CallRoom
        {
            RoomCode = roomCode,
            Title = dto.Title.Trim(),
            HostUserId = userId,
            Status = "Active",
            MaxParticipants = dto.MaxParticipants,
            CreatedAt = DateTime.UtcNow
        };

        _context.CallRooms.Add(room);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[CALL ROOM CREATED] Phòng '{RoomCode}' ('{Title}') được tạo bởi Host: {Username}",
            roomCode, room.Title, user.UserName);

        return Ok(new CallRoomDto
        {
            Id = room.Id,
            RoomCode = room.RoomCode,
            Title = room.Title,
            HostUserId = room.HostUserId,
            HostName = user.FullName ?? user.UserName,
            Status = room.Status,
            MaxParticipants = room.MaxParticipants,
            ActiveParticipantCount = 0,
            CreatedAt = room.CreatedAt
        });
    }

    /// <summary>
    /// 2. Lấy thông tin phòng gọi và danh sách người tham gia
    /// </summary>
    [HttpGet("rooms/{roomCode}")]
    public async Task<IActionResult> GetRoomDetails(string roomCode)
    {
        var room = await _context.CallRooms
            .Include(r => r.HostUser)
            .Include(r => r.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

        if (room == null)
            return NotFound(new { message = $"Không tìm thấy phòng gọi '{roomCode}'" });

        var participants = room.Participants
            .Select(p => new CallParticipantDto
            {
                UserId = p.UserId,
                Username = p.User?.UserName ?? "N/A",
                FullName = p.User?.FullName ?? p.User?.UserName ?? "N/A",
                ConnectionId = p.ConnectionId,
                JoinedAt = p.JoinedAt,
                LeftAt = p.LeftAt,
                IsMuted = p.IsMuted,
                IsCameraOff = p.IsCameraOff,
                IsOnline = p.LeftAt == null
            })
            .ToList();

        var activeCount = participants.Count(p => p.IsOnline);

        return Ok(new CallRoomDto
        {
            Id = room.Id,
            RoomCode = room.RoomCode,
            Title = room.Title,
            HostUserId = room.HostUserId,
            HostName = room.HostUser?.FullName ?? room.HostUser?.UserName,
            Status = room.Status,
            MaxParticipants = room.MaxParticipants,
            ActiveParticipantCount = activeCount,
            CreatedAt = room.CreatedAt,
            EndedAt = room.EndedAt,
            Participants = participants
        });
    }

    /// <summary>
    /// 3. LƯU Ý 2: Kết thúc phòng gọi nhóm
    /// Phân quyền chặt chẽ: Chỉ HostUserId hoặc Admin mới được phép đóng phòng.
    /// Broadcast sự kiện CallEnded đến tất cả client trong phòng.
    /// </summary>
    [HttpPost("rooms/{roomCode}/end")]
    public async Task<IActionResult> EndCallRoom(string roomCode)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Không xác thực được danh tính" });

        var room = await _context.CallRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        if (room == null)
            return NotFound(new { message = $"Không tìm thấy phòng gọi '{roomCode}'" });

        if (room.Status == "Ended")
            return BadRequest(new { message = "Phòng gọi này đã kết thúc trước đó." });

        // Kiểm tra phân quyền: Phải là HostUserId hoặc Role Admin
        var isAdmin = User.IsInRole("Admin");
        var isHost = room.HostUserId == userId;

        if (!isHost && !isAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Bạn không có quyền kết thúc cuộc gọi này. Chỉ chủ phòng (Host) hoặc Quản trị viên (Admin) mới có quyền đóng phòng."
            });
        }

        // Cập nhật trạng thái phòng
        room.Status = "Ended";
        room.EndedAt = DateTime.UtcNow;

        // Cập nhật LeftAt cho các thành viên chưa rời
        var openParticipants = await _context.CallParticipants
            .Where(p => p.RoomId == room.Id && p.LeftAt == null)
            .ToListAsync();
        foreach (var p in openParticipants)
        {
            p.LeftAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Broadcast CallEnded qua SignalR Hub tới tất cả client trong phòng
        var groupName = $"call_room_{roomCode}";
        await _hubContext.Clients.Group(groupName).SendAsync("CallEnded", new
        {
            roomCode,
            message = "Cuộc gọi đã được kết thúc bởi chủ phòng/quản trị viên.",
            endedAt = room.EndedAt
        });

        _logger.LogInformation("[CALL ROOM ENDED VIA API] Phòng '{RoomCode}' đã đóng bởi UserId: {UserId} (IsAdmin: {IsAdmin})",
            roomCode, userId, isAdmin);

        return Ok(new EndCallResponseDto
        {
            RoomCode = room.RoomCode,
            Message = "Đã kết thúc phòng gọi video nhóm thành công.",
            EndedAt = room.EndedAt.Value
        });
    }

    /// <summary>
    /// 4. Lấy lịch sử các cuộc gọi nhóm đã tham gia hoặc tạo
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetCallHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var rooms = await _context.CallRooms
            .Include(r => r.HostUser)
            .Include(r => r.Participants)
            .Where(r => r.HostUserId == userId || r.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new CallRoomDto
            {
                Id = r.Id,
                RoomCode = r.RoomCode,
                Title = r.Title,
                HostUserId = r.HostUserId,
                HostName = r.HostUser != null ? (r.HostUser.FullName ?? r.HostUser.UserName) : null,
                Status = r.Status,
                MaxParticipants = r.MaxParticipants,
                ActiveParticipantCount = r.Participants.Count(p => p.LeftAt == null),
                CreatedAt = r.CreatedAt,
                EndedAt = r.EndedAt
            })
            .ToListAsync();

        return Ok(rooms);
    }
}
