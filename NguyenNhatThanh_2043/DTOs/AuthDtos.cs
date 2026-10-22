using System.ComponentModel.DataAnnotations;

namespace NguyenNhatThanh_2043.DTOs;

public class LoginStep1Dto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    public string Password { get; set; } = string.Empty;
}

public class LoginStep1ResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    // Để hỗ trợ kiểm thử nếu dev muốn xem trực tiếp OTP trong response khi test qua Swagger:
    public string? OtpForTesting { get; set; }
}

public class VerifyOtpDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã OTP không được để trống")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 chữ số")]
    public string OtpCode { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TargetRoute { get; set; } = string.Empty;
}
