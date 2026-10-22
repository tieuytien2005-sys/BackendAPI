using System.ComponentModel.DataAnnotations;

namespace NguyenNhatThanh_2043.DTOs;

public class UpdateLocationDto
{
    [Required(ErrorMessage = "Vĩ độ (Latitude) không được để trống")]
    [Range(-90.0, 90.0, ErrorMessage = "Vĩ độ hợp lệ từ -90 đến 90")]
    public double Latitude { get; set; }

    [Required(ErrorMessage = "Kinh độ (Longitude) không được để trống")]
    [Range(-180.0, 180.0, ErrorMessage = "Kinh độ hợp lệ từ -180 đến 180")]
    public double Longitude { get; set; }

    [MaxLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự")]
    public string? Address { get; set; }

    public double? Speed { get; set; }
}

public class ToggleSharingDto
{
    [Required]
    public bool IsSharing { get; set; }
}

public class UserLocationDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public bool IsSharingLocation { get; set; }

    /// <summary>
    /// Thời gian cập nhật theo chuẩn UTC (ISO 8601)
    /// </summary>
    public DateTime? LastLocationUpdatedAt { get; set; }

    /// <summary>
    /// Khoảng cách tính bằng km so với tọa độ của người tìm kiếm (nếu có)
    /// </summary>
    public double? DistanceInKm { get; set; }
}

public class LocationHistoryDto
{
    public long Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public double? Speed { get; set; }

    /// <summary>
    /// Thời điểm ghi nhận mốc vị trí theo chuẩn UTC (ISO 8601)
    /// </summary>
    public DateTime RecordedAt { get; set; }
}

public class NearbyQueryDto
{
    [Range(-90.0, 90.0)]
    public double? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double? Longitude { get; set; }

    /// <summary>
    /// Bán kính tìm kiếm (km), mặc định 10km
    /// </summary>
    public double RadiusInKm { get; set; } = 10.0;

    /// <summary>
    /// Lọc theo Role nếu cần (Admin, Staff, Customer)
    /// </summary>
    public string? Role { get; set; }
}
