using Microsoft.AspNetCore.Identity;

namespace NguyenNhatThanh_2043.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiryTime { get; set; }

    /// <summary>
    /// Chuỗi JSON biểu diễn vector đặc trưng khuôn mặt (Face Embedding Float Array).
    /// Cấu hình kiểu nvarchar(max) trong DbContext để tránh tràn chuỗi.
    /// </summary>
    public string? FaceData { get; set; }

    /// <summary>
    /// Thời điểm đăng ký khuôn mặt
    /// </summary>
    public DateTime? FaceRegisteredAt { get; set; }

    /// <summary>
    /// Đường dẫn hoặc chuỗi Base64 ảnh khuôn mặt đại diện (nếu có)
    /// </summary>
    public string? FaceImageUrl { get; set; }

    // ================= CHIA SẺ VỊ TRÍ (LOCATION SHARING) =================

    /// <summary>
    /// Vĩ độ GPS hiện tại của người dùng
    /// </summary>
    public double? CurrentLatitude { get; set; }

    /// <summary>
    /// Kinh độ GPS hiện tại của người dùng
    /// </summary>
    public double? CurrentLongitude { get; set; }

    /// <summary>
    /// Tên địa chỉ hoặc mô tả vị trí hiện tại
    /// </summary>
    public string? LocationAddress { get; set; }

    /// <summary>
    /// Trạng thái bật/tắt chia sẻ vị trí (mặc định false để bảo vệ quyền riêng tư)
    /// </summary>
    public bool IsSharingLocation { get; set; } = false;

    /// <summary>
    /// Thời điểm cập nhật vị trí gần nhất (chuẩn hóa DateTime.UtcNow)
    /// </summary>
    public DateTime? LastLocationUpdatedAt { get; set; }

    /// <summary>
    /// Quan hệ 1-N với bảng lịch sử di chuyển
    /// </summary>
    public ICollection<UserLocationHistory> LocationHistories { get; set; } = new List<UserLocationHistory>();
}
