using NguyenNhatThanh_2043.DTOs;

namespace NguyenNhatThanh_2043.Services;

public interface ILocationService
{
    /// <summary>
    /// Tính khoảng cách địa lý giữa 2 tọa độ GPS theo công thức Haversine (đơn vị: km)
    /// </summary>
    double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2);

    /// <summary>
    /// Cập nhật vị trí hiện tại của người dùng.
    /// Có cơ chế Throttling: Chỉ ghi lịch sử khi di chuyển >= 10 mét hoặc đã qua >= 30 giây.
    /// </summary>
    Task<UserLocationDto> UpdateLocationAsync(string userId, UpdateLocationDto dto);

    /// <summary>
    /// Bật hoặc tắt trạng thái chia sẻ vị trí
    /// </summary>
    Task<bool> ToggleSharingAsync(string userId, bool isSharing);

    /// <summary>
    /// Lấy danh sách người dùng đang bật chia sẻ vị trí (hỗ trợ lọc theo Role và bán kính Haversine)
    /// </summary>
    Task<List<UserLocationDto>> GetActiveSharingUsersAsync(string currentUserId, NearbyQueryDto query);

    /// <summary>
    /// Lấy vị trí của một người dùng cụ thể (kiểm tra quyền riêng tư hoặc Admin)
    /// </summary>
    Task<UserLocationDto?> GetUserLocationAsync(string targetUserId, string currentUserId, bool isAdmin);

    /// <summary>
    /// Lấy danh sách lịch sử vị trí (breadcrumb) của người dùng
    /// </summary>
    Task<List<LocationHistoryDto>> GetLocationHistoryAsync(string targetUserId, string currentUserId, bool isAdmin, int limit = 50);

    /// <summary>
    /// Xóa toàn bộ lịch sử vị trí của chính người dùng
    /// </summary>
    Task<bool> ClearLocationHistoryAsync(string userId);
}
