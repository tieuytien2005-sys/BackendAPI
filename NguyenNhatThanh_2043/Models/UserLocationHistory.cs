namespace NguyenNhatThanh_2043.Models;

public class UserLocationHistory
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? Address { get; set; }
    public double? Speed { get; set; }

    /// <summary>
    /// Thời điểm ghi nhận mốc vị trí (luôn lưu chuẩn DateTime.UtcNow)
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
