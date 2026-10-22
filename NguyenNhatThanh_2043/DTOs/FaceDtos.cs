using System.ComponentModel.DataAnnotations;

namespace NguyenNhatThanh_2043.DTOs;

public class RegisterFaceDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vector khuôn mặt (FaceEmbedding) không được để trống")]
    public List<float> FaceEmbedding { get; set; } = new();

    /// <summary>
    /// Ảnh khuôn mặt dạng base64 hoặc URL (tùy chọn) để lưu trữ hiển thị
    /// </summary>
    public string? FaceImageUrl { get; set; }
}

public class LoginFaceDto
{
    /// <summary>
    /// Tùy chọn: Nếu truyền Username -> Chế độ 1:1 (Xác minh chính xác).
    /// Nếu để trống -> Chế độ 1:N (Tự động nhận diện tài khoản trong cơ sở dữ liệu).
    /// </summary>
    public string? Username { get; set; }

    [Required(ErrorMessage = "Vector khuôn mặt (FaceEmbedding) không được để trống")]
    public List<float> FaceEmbedding { get; set; } = new();

    /// <summary>
    /// Ngưỡng nhận diện Cosine Similarity linh hoạt (mặc định 0.70 để tránh False Rejection khi chụp lệch góc/thiếu sáng).
    /// Client có thể truyền từ 0.50 đến 0.95 tùy kịch bản demo.
    /// </summary>
    public float? Threshold { get; set; } = 0.70f;
}

public class FaceStatusDto
{
    public string Username { get; set; } = string.Empty;
    public bool IsFaceRegistered { get; set; }
    public DateTime? RegisteredAt { get; set; }
    public string? FaceImageUrl { get; set; }
}

public class FaceLoginResponseDto : AuthResponseDto
{
    /// <summary>
    /// Điểm độ tương đồng Cosine Similarity tính được (từ 0.00 đến 1.00)
    /// </summary>
    public float SimilarityScore { get; set; }
}
