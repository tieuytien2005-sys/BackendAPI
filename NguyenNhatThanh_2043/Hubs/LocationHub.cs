using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NguyenNhatThanh_2043.DTOs;
using NguyenNhatThanh_2043.Services;

namespace NguyenNhatThanh_2043.Hubs;

[Authorize]
public class LocationHub : Hub
{
    private readonly ILocationService _locationService;
    private readonly ILogger<LocationHub> _logger;

    public LocationHub(ILocationService locationService, ILogger<LocationHub> logger)
    {
        _locationService = locationService;
        _logger = logger;
    }

    /// <summary>
    /// Client gọi qua WebSocket để cập nhật vị trí thời gian thực.
    /// Hub vừa lưu DB/áp dụng Throttling vừa broadcast ngay tới các client khác.
    /// </summary>
    public async Task SendLocationUpdate(UpdateLocationDto dto)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Không thể xác thực danh tính người dùng.");
        }

        // Cập nhật vị trí và lịch sử qua Service
        var updatedLocation = await _locationService.UpdateLocationAsync(userId, dto);

        // Chỉ phát broadcast nếu người dùng đang bật IsSharingLocation
        if (updatedLocation.IsSharingLocation)
        {
            // 1. Broadcast sự kiện OnLocationReceived tới tất cả client đang mở bản đồ
            await Clients.Others.SendAsync("OnLocationReceived", updatedLocation);

            // 2. Gửi riêng tới Group đang theo dõi cụ thể người dùng này (nếu có)
            await Clients.Group($"tracking_{userId}").SendAsync("OnUserLocationUpdated", updatedLocation);

            _logger.LogInformation("[SIGNALR LOCATION] Phát tọa độ thời gian thực của '{Username}': ({Lat}, {Lng})",
                updatedLocation.Username, dto.Latitude, dto.Longitude);
        }
    }

    /// <summary>
    /// Client đăng ký theo dõi vị trí của một tài khoản cụ thể (ví dụ khách theo dõi tài xế/nhân viên)
    /// </summary>
    public async Task JoinTrackingGroup(string targetUserId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tracking_{targetUserId}");
        _logger.LogInformation("Connection {ConnectionId} tham gia theo dõi targetUserId: {TargetUserId}",
            Context.ConnectionId, targetUserId);
    }

    /// <summary>
    /// Rời khỏi nhóm theo dõi
    /// </summary>
    public async Task LeaveTrackingGroup(string targetUserId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tracking_{targetUserId}");
    }

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name ?? "Anonymous";
        _logger.LogInformation("[SIGNALR CONNECT] Người dùng '{Username}' đã kết nối LocationHub (ConnectionId: {ConnectionId})",
            username, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User?.Identity?.Name ?? "Anonymous";
        _logger.LogInformation("[SIGNALR DISCONNECT] Người dùng '{Username}' ngắt kết nối LocationHub", username);
        await base.OnDisconnectedAsync(exception);
    }
}
