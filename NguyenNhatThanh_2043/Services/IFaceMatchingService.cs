using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Services;

public record FaceMatchResult(ApplicationUser? User, float SimilarityScore, bool IsMatch);

public interface IFaceMatchingService
{
    /// <summary>
    /// Tính toán độ tương đồng Cosine giữa 2 vector khuôn mặt.
    /// Có xử lý an toàn ngoại lệ chia cho 0 và kiểm tra kích thước.
    /// </summary>
    float CalculateCosineSimilarity(IReadOnlyList<float>? v1, IReadOnlyList<float>? v2);

    /// <summary>
    /// Chế độ 1:1 - Xác minh khuôn mặt đối với một người dùng cụ thể.
    /// </summary>
    Task<FaceMatchResult> VerifyFaceAsync(string username, IReadOnlyList<float> queryEmbedding, float threshold);

    /// <summary>
    /// Chế độ 1:N - Tìm kiếm tài khoản có khuôn mặt khớp nhất trong database.
    /// Tối ưu hiệu năng: Chỉ tải các cột Id, UserName, FaceData của các user có FaceData != null.
    /// </summary>
    Task<FaceMatchResult> FindBestMatchAsync(IReadOnlyList<float> queryEmbedding, float threshold);
}
