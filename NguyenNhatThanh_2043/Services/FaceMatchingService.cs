using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Services;

public class FaceMatchingService : IFaceMatchingService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FaceMatchingService> _logger;

    public FaceMatchingService(
        UserManager<ApplicationUser> userManager,
        ILogger<FaceMatchingService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Tính toán độ tương đồng Cosine giữa 2 vector đặc trưng khuôn mặt:
    /// Cosine(A, B) = (A . B) / (||A|| * ||B||)
    /// Đã xử lý an toàn:
    /// - Kiểm tra null và kích thước 2 vector (v1.Count == v2.Count).
    /// - Kiểm tra normA == 0 hoặc normB == 0 để tránh lỗi chia cho 0 (DivideByZeroException).
    /// </summary>
    public float CalculateCosineSimilarity(IReadOnlyList<float>? v1, IReadOnlyList<float>? v2)
    {
        if (v1 == null || v2 == null || v1.Count == 0 || v2.Count == 0)
        {
            return 0f;
        }

        if (v1.Count != v2.Count)
        {
            _logger.LogWarning("Kích thước 2 vector khuôn mặt không khớp ({Count1} vs {Count2}).", v1.Count, v2.Count);
            return 0f;
        }

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < v1.Count; i++)
        {
            float a = v1[i];
            float b = v2[i];

            dotProduct += a * b;
            normA += a * a;
            normB += b * b;
        }

        // Phòng tránh lỗi chia cho 0 nếu vector có độ dài bằng 0
        if (normA <= 0f || normB <= 0f)
        {
            return 0f;
        }

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    /// <summary>
    /// Chế độ 1:1 - Xác minh khuôn mặt đối với một User cụ thể
    /// </summary>
    public async Task<FaceMatchResult> VerifyFaceAsync(string username, IReadOnlyList<float> queryEmbedding, float threshold)
    {
        var user = await _userManager.FindByNameAsync(username) 
                   ?? await _userManager.FindByEmailAsync(username);

        if (user == null || string.IsNullOrEmpty(user.FaceData))
        {
            return new FaceMatchResult(null, 0f, false);
        }

        List<float>? storedEmbedding;
        try
        {
            storedEmbedding = JsonSerializer.Deserialize<List<float>>(user.FaceData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi giải mã FaceData JSON của người dùng {Username}", username);
            return new FaceMatchResult(null, 0f, false);
        }

        if (storedEmbedding == null)
        {
            return new FaceMatchResult(null, 0f, false);
        }

        float score = CalculateCosineSimilarity(queryEmbedding, storedEmbedding);
        bool isMatch = score >= threshold;

        _logger.LogInformation("[FACE 1:1] User: {Username} | Score: {Score:F4} | Threshold: {Threshold} | Match: {IsMatch}",
            user.UserName, score, threshold, isMatch);

        return new FaceMatchResult(user, score, isMatch);
    }

    /// <summary>
    /// Chế độ 1:N - Tìm tài khoản có khuôn mặt khớp nhất trong toàn hệ thống.
    /// Tối ưu hiệu năng:
    /// - Chỉ truy vấn AsNoTracking các User có FaceData != null.
    /// - Chỉ Select đúng 3 cột (Id, UserName, FaceData) lên RAM để tính toán.
    /// </summary>
    public async Task<FaceMatchResult> FindBestMatchAsync(IReadOnlyList<float> queryEmbedding, float threshold)
    {
        // 1. Tối ưu hiệu năng: Dùng LINQ lọc từ database chỉ lấy các tài khoản đã đăng ký FaceData
        var candidateUsers = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.FaceData != null && u.FaceData != "")
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.FaceData
            })
            .ToListAsync();

        if (candidateUsers.Count == 0)
        {
            _logger.LogInformation("[FACE 1:N] Chưa có người dùng nào đăng ký FaceData trong hệ thống.");
            return new FaceMatchResult(null, 0f, false);
        }

        string? bestUserId = null;
        float maxScore = -1f;

        // 2. Duyệt tính toán Cosine Similarity trên RAM
        foreach (var candidate in candidateUsers)
        {
            try
            {
                var storedEmbedding = JsonSerializer.Deserialize<List<float>>(candidate.FaceData!);
                if (storedEmbedding == null) continue;

                float score = CalculateCosineSimilarity(queryEmbedding, storedEmbedding);
                if (score > maxScore)
                {
                    maxScore = score;
                    bestUserId = candidate.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bỏ qua bản ghi FaceData lỗi của User Id {UserId}", candidate.Id);
            }
        }

        _logger.LogInformation("[FACE 1:N] Điểm cao nhất tìm được: {MaxScore:F4} (Ngưỡng yêu cầu: {Threshold})", maxScore, threshold);

        if (bestUserId != null && maxScore >= threshold)
        {
            // Tải thông tin người dùng hoàn chỉnh sau khi đã xác định đúng tài khoản khớp
            var matchedUser = await _userManager.FindByIdAsync(bestUserId);
            return new FaceMatchResult(matchedUser, maxScore, true);
        }

        return new FaceMatchResult(null, maxScore, false);
    }
}
