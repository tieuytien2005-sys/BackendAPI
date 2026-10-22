using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NguyenNhatThanh_2043.DTOs;
using NguyenNhatThanh_2043.Hubs;
using NguyenNhatThanh_2043.Services;

namespace NguyenNhatThanh_2043.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;
    private readonly IHubContext<LocationHub> _hubContext;
    private readonly ILogger<LocationsController> _logger;

    public LocationsController(
        ILocationService locationService,
        IHubContext<LocationHub> hubContext,
        ILogger<LocationsController> logger)
    {
        _locationService = locationService;
        _hubContext = hubContext;
        _logger = logger;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Chức năng 5.b: Cập nhật vị trí hiện tại của người dùng
    /// POST /api/locations/update
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updatedLocation = await _locationService.UpdateLocationAsync(CurrentUserId, model);

            // Nếu người dùng đang bật chia sẻ, đồng thời broadcast qua SignalR Hub tới các client đang theo dõi
            if (updatedLocation.IsSharingLocation)
            {
                await _hubContext.Clients.All.SendAsync("OnLocationReceived", updatedLocation);
                await _hubContext.Clients.Group($"tracking_{CurrentUserId}").SendAsync("OnUserLocationUpdated", updatedLocation);
            }

            return Ok(new
            {
                success = true,
                message = "Cập nhật vị trí thành công!",
                data = updatedLocation
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Chức năng 5.b: Bật hoặc tắt trạng thái chia sẻ vị trí của bản thân
    /// POST /api/locations/toggle-sharing
    /// </summary>
    [HttpPost("toggle-sharing")]
    public async Task<IActionResult> ToggleSharing([FromBody] ToggleSharingDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var success = await _locationService.ToggleSharingAsync(CurrentUserId, model.IsSharing);
            return Ok(new
            {
                success = success,
                isSharing = model.IsSharing,
                message = model.IsSharing 
                    ? "Đã bật chia sẻ vị trí. Các người dùng khác có thể nhìn thấy bạn trên bản đồ." 
                    : "Đã tắt chia sẻ vị trí để bảo vệ quyền riêng tư."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Chức năng 5.b: Lấy danh sách người dùng đang bật chia sẻ vị trí
    /// GET /api/locations/active?latitude=...&longitude=...&radiusInKm=10&role=Staff
    /// Hỗ trợ tính khoảng cách Haversine và lọc theo bán kính / Role
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSharingUsers([FromQuery] NearbyQueryDto query)
    {
        var users = await _locationService.GetActiveSharingUsersAsync(CurrentUserId, query);
        return Ok(users);
    }

    /// <summary>
    /// Chức năng 5.b: Xem vị trí hiện tại của một người dùng cụ thể
    /// GET /api/locations/user/{userId}
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserLocation(string userId)
    {
        var userLocation = await _locationService.GetUserLocationAsync(userId, CurrentUserId, IsAdmin);
        if (userLocation == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng hoặc người dùng đã tắt chia sẻ vị trí." });
        }

        return Ok(userLocation);
    }

    /// <summary>
    /// Chức năng 5.b: Lấy danh sách lịch sử vị trí (Breadcrumbs / Lộ trình)
    /// GET /api/locations/history/{userId}?limit=50
    /// </summary>
    [HttpGet("history/{userId}")]
    public async Task<IActionResult> GetLocationHistory(string userId, [FromQuery] int limit = 50)
    {
        // Giới hạn số lượng điểm trả về tối đa 200 điểm
        int clampedLimit = Math.Clamp(limit, 1, 200);

        var history = await _locationService.GetLocationHistoryAsync(userId, CurrentUserId, IsAdmin, clampedLimit);
        return Ok(history);
    }

    /// <summary>
    /// Chức năng 5.b: Xóa toàn bộ lịch sử vị trí của chính mình
    /// DELETE /api/locations/history
    /// </summary>
    [HttpDelete("history")]
    public async Task<IActionResult> ClearMyHistory()
    {
        await _locationService.ClearLocationHistoryAsync(CurrentUserId);
        return Ok(new { success = true, message = "Đã xóa toàn bộ lịch sử vị trí của bạn." });
    }
}
