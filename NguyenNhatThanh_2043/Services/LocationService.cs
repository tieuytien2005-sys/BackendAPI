using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NguyenNhatThanh_2043.Data;
using NguyenNhatThanh_2043.DTOs;
using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Services;

public class LocationService : ILocationService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LocationService> _logger;

    public LocationService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<LocationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Công thức Haversine tính khoảng cách địa lý giữa 2 điểm GPS (đơn vị: km)
    /// Bán kính trái đất R = 6371.0 km
    /// </summary>
    public double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);

        double radLat1 = DegreesToRadians(lat1);
        double radLat2 = DegreesToRadians(lat2);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(radLat1) * Math.Cos(radLat2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Cập nhật vị trí tức thời và áp dụng cơ chế Throttling chống spam ghi bảng UserLocationHistory:
    /// - Cập nhật ApplicationUser (tọa độ hiện tại, thời gian UTC) luôn diễn ra.
    /// - Chỉ ghi dòng mới vào UserLocationHistory nếu:
    ///   + Đang bật IsSharingLocation == true
    ///   + VÀ (Di chuyển >= 10 mét so với điểm gần nhất HOẶC thời gian đã trôi qua >= 30 giây).
    /// </summary>
    public async Task<UserLocationDto> UpdateLocationAsync(string userId, UpdateLocationDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với Id: {userId}");
        }

        var utcNow = DateTime.UtcNow;

        // 1. Luôn cập nhật vị trí hiện tại trên ApplicationUser
        user.CurrentLatitude = dto.Latitude;
        user.CurrentLongitude = dto.Longitude;
        user.LocationAddress = dto.Address;
        user.LastLocationUpdatedAt = utcNow;

        await _userManager.UpdateAsync(user);

        // 2. Cơ chế Throttling ghi lịch sử (chỉ khi user đang bật chia sẻ)
        if (user.IsSharingLocation)
        {
            var lastHistory = await _context.LocationHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.RecordedAt)
                .FirstOrDefaultAsync();

            bool shouldRecordHistory = false;

            if (lastHistory == null)
            {
                shouldRecordHistory = true;
            }
            else
            {
                var elapsedSeconds = (utcNow - lastHistory.RecordedAt).TotalSeconds;
                var distanceKm = CalculateHaversineDistanceKm(lastHistory.Latitude, lastHistory.Longitude, dto.Latitude, dto.Longitude);

                // Di chuyển >= 10 mét (0.010 km) HOẶC đã trôi qua >= 30 giây
                if (distanceKm >= 0.010 || elapsedSeconds >= 30.0)
                {
                    shouldRecordHistory = true;
                    _logger.LogInformation("[LOCATION THROTTLING] Ghi lịch sử UserId: {UserId} | Distance: {Distance:F4} km | Elapsed: {Elapsed:F1}s",
                        userId, distanceKm, elapsedSeconds);
                }
            }

            if (shouldRecordHistory)
            {
                var historyRecord = new UserLocationHistory
                {
                    UserId = userId,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    Address = dto.Address,
                    Speed = dto.Speed,
                    RecordedAt = utcNow // Chuẩn hóa UTC
                };

                _context.LocationHistories.Add(historyRecord);
                await _context.SaveChangesAsync();
            }
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserLocationDto
        {
            UserId = user.Id,
            Username = user.UserName ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            Role = roles.FirstOrDefault() ?? "Customer",
            Latitude = user.CurrentLatitude,
            Longitude = user.CurrentLongitude,
            Address = user.LocationAddress,
            IsSharingLocation = user.IsSharingLocation,
            LastLocationUpdatedAt = user.LastLocationUpdatedAt
        };
    }

    /// <summary>
    /// Bật hoặc tắt trạng thái chia sẻ vị trí
    /// </summary>
    public async Task<bool> ToggleSharingAsync(string userId, bool isSharing)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy người dùng với Id: {userId}");
        }

        user.IsSharingLocation = isSharing;
        user.LastLocationUpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    /// <summary>
    /// Lấy danh sách người dùng đang bật chia sẻ vị trí (hỗ trợ lọc theo Role và bán kính Haversine)
    /// </summary>
    public async Task<List<UserLocationDto>> GetActiveSharingUsersAsync(string currentUserId, NearbyQueryDto query)
    {
        // Lọc người dùng đang bật chia sẻ và đã có tọa độ GPS
        var sharingUsers = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.IsSharingLocation && u.CurrentLatitude != null && u.CurrentLongitude != null)
            .ToListAsync();

        var resultList = new List<UserLocationDto>();

        foreach (var user in sharingUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "Customer";

            // Lọc theo Role nếu có chỉ định
            if (!string.IsNullOrWhiteSpace(query.Role) &&
                !primaryRole.Equals(query.Role, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double? distance = null;
            if (query.Latitude.HasValue && query.Longitude.HasValue &&
                user.CurrentLatitude.HasValue && user.CurrentLongitude.HasValue)
            {
                distance = CalculateHaversineDistanceKm(
                    query.Latitude.Value, query.Longitude.Value,
                    user.CurrentLatitude.Value, user.CurrentLongitude.Value);

                // Lọc theo bán kính nếu khoảng cách vượt quá
                if (distance.Value > query.RadiusInKm)
                {
                    continue;
                }
            }

            resultList.Add(new UserLocationDto
            {
                UserId = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                Role = primaryRole,
                Latitude = user.CurrentLatitude,
                Longitude = user.CurrentLongitude,
                Address = user.LocationAddress,
                IsSharingLocation = user.IsSharingLocation,
                LastLocationUpdatedAt = user.LastLocationUpdatedAt,
                DistanceInKm = distance.HasValue ? Math.Round(distance.Value, 2) : null
            });
        }

        // Sắp xếp người ở gần nhất lên đầu nếu có tọa độ tìm kiếm
        if (query.Latitude.HasValue && query.Longitude.HasValue)
        {
            resultList = resultList.OrderBy(u => u.DistanceInKm).ToList();
        }

        return resultList;
    }

    /// <summary>
    /// Lấy thông tin vị trí của một người dùng cụ thể
    /// </summary>
    public async Task<UserLocationDto?> GetUserLocationAsync(string targetUserId, string currentUserId, bool isAdmin)
    {
        var user = await _userManager.FindByIdAsync(targetUserId);
        if (user == null) return null;

        // Nếu không phải Admin và không phải chính mình, người đó phải đang bật chia sẻ vị trí
        if (!isAdmin && targetUserId != currentUserId && !user.IsSharingLocation)
        {
            return null; // Quyền riêng tư: Không trả về tọa độ nếu tắt chia sẻ
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserLocationDto
        {
            UserId = user.Id,
            Username = user.UserName ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            Role = roles.FirstOrDefault() ?? "Customer",
            Latitude = user.CurrentLatitude,
            Longitude = user.CurrentLongitude,
            Address = user.LocationAddress,
            IsSharingLocation = user.IsSharingLocation,
            LastLocationUpdatedAt = user.LastLocationUpdatedAt
        };
    }

    /// <summary>
    /// Lấy danh sách lịch sử vị trí (Breadcrumbs)
    /// </summary>
    public async Task<List<LocationHistoryDto>> GetLocationHistoryAsync(string targetUserId, string currentUserId, bool isAdmin, int limit = 50)
    {
        // Chỉ Admin hoặc chính chủ mới có quyền xem lịch sử di chuyển
        if (!isAdmin && targetUserId != currentUserId)
        {
            return new List<LocationHistoryDto>();
        }

        var histories = await _context.LocationHistories
            .AsNoTracking()
            .Where(h => h.UserId == targetUserId)
            .OrderByDescending(h => h.RecordedAt)
            .Take(limit)
            .Select(h => new LocationHistoryDto
            {
                Id = h.Id,
                Latitude = h.Latitude,
                Longitude = h.Longitude,
                Address = h.Address,
                Speed = h.Speed,
                RecordedAt = h.RecordedAt // ISO 8601 UTC
            })
            .ToListAsync();

        return histories;
    }

    /// <summary>
    /// Xóa toàn bộ lịch sử vị trí của chính người dùng
    /// </summary>
    public async Task<bool> ClearLocationHistoryAsync(string userId)
    {
        var histories = await _context.LocationHistories
            .Where(h => h.UserId == userId)
            .ToListAsync();

        if (histories.Count > 0)
        {
            _context.LocationHistories.RemoveRange(histories);
            await _context.SaveChangesAsync();
        }

        return true;
    }
}
