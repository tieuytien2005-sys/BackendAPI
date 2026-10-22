using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NguyenNhatThanh_2043.DTOs;
using NguyenNhatThanh_2043.Models;
using NguyenNhatThanh_2043.Services;

namespace NguyenNhatThanh_2043.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly IFaceMatchingService _faceMatchingService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        IFaceMatchingService faceMatchingService,
        IEmailService emailService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _faceMatchingService = faceMatchingService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Mục 1: Đăng ký tài khoản qua Email
    /// POST: /api/auth/register
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Kiểm tra xem Email đã tồn tại chưa
        var existingEmail = await _userManager.FindByEmailAsync(model.Email);
        if (existingEmail != null)
        {
            return BadRequest(new { message = "Email này đã được sử dụng trong hệ thống." });
        }

        // Kiểm tra xem Username đã tồn tại chưa
        var existingUser = await _userManager.FindByNameAsync(model.Username);
        if (existingUser != null)
        {
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại, vui lòng chọn tên khác." });
        }

        // Chuẩn hóa Role (mặc định Customer nếu nhập sai hoặc để trống)
        var role = string.IsNullOrWhiteSpace(model.Role) ? "Customer" : model.Role.Trim();
        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Staff", "Customer" };
        if (!allowedRoles.Contains(role))
        {
            return BadRequest(new { message = $"Vai trò '{model.Role}' không hợp lệ. Chỉ chấp nhận: Admin, Staff, Customer." });
        }

        // Đảm bảo Role đã tồn tại trong database
        var matchedRole = allowedRoles.First(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
        if (!await _roleManager.RoleExistsAsync(matchedRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(matchedRole));
        }

        var user = new ApplicationUser
        {
            UserName = model.Username,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Đăng ký không thành công.", errors = result.Errors });
        }

        // Gán Role tương ứng
        await _userManager.AddToRoleAsync(user, matchedRole);

        return Ok(new
        {
            message = "Đăng ký tài khoản thành công!",
            userId = user.Id,
            username = user.UserName,
            email = user.Email,
            role = matchedRole
        });
    }

    /// <summary>
    /// Mục 2 - Bước 1: Đăng nhập Username + Password -> Sinh mã OTP
    /// POST: /api/auth/login-step1
    /// </summary>
    [HttpPost("login-step1")]
    public async Task<IActionResult> LoginStep1([FromBody] LoginStep1Dto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByNameAsync(model.Username) 
                   ?? await _userManager.FindByEmailAsync(model.Username);

        if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
        {
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
        }

        // Sinh mã OTP 6 chữ số ngẫu nhiên
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        user.OtpCode = otpCode;
        user.OtpExpiryTime = DateTime.UtcNow.AddMinutes(5); // Hết hạn sau 5 phút

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return StatusCode(500, new { message = "Không thể tạo mã OTP, vui lòng thử lại." });
        }

        // Xác định email nhận OTP (nếu là tài khoản admin hoặc email test example.com thì chuyển về huamoc2005@gmail.com)
        var recipientEmail = !string.IsNullOrWhiteSpace(user.Email) && !user.Email.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase)
            ? user.Email
            : "huamoc2005@gmail.com";

        // Gửi mã OTP về email thật qua Gmail SMTP
        try
        {
            await _emailService.SendOtpEmailAsync(recipientEmail, otpCode, user.FullName ?? user.UserName ?? "Người dùng");
            _logger.LogInformation("[EMAIL SENT] Đã gửi thành công mã OTP thật đến email: {Email}", recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email OTP thật tới {Email}: {Message}", recipientEmail, ex.Message);
            return StatusCode(500, new
            {
                message = $"Không thể gửi mã OTP về email ({recipientEmail}). Vui lòng kiểm tra kết nối mạng hoặc thông tin xác thực Gmail.",
                detail = ex.Message
            });
        }

        // Vẫn ghi log ra console để hỗ trợ đối chiếu / gỡ lỗi
        _logger.LogInformation("=================================================");
        _logger.LogInformation("[OTP VERIFICATION] Username: {Username} | OTP: {OtpCode} | Expiry: {Expiry} | Sent to: {Email}", 
            user.UserName, otpCode, user.OtpExpiryTime, recipientEmail);
        _logger.LogInformation("=================================================");
        Console.WriteLine($"\n[OTP EMAIL SENT] >>> User: {user.UserName} | OTP Code: {otpCode} | Sent to Email: {recipientEmail} (Hết hạn sau 5 phút) <<<\n");

        return Ok(new LoginStep1ResponseDto
        {
            Success = true,
            Message = $"Mã OTP đã được gửi về email {MaskEmail(recipientEmail)}. Vui lòng kiểm tra hộp thư đến (hoặc hòm thư Spam/Rác).",
            Username = user.UserName!,
            OtpForTesting = otpCode // Trả kèm để tiện kiểm thử ngay trên Swagger UI
        });
    }

    /// <summary>
    /// Mục 2 - Bước 2: Xác thực OTP & Mục 3: Cấp JWT + Điều hướng Role
    /// POST: /api/auth/verify-otp
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null)
        {
            return BadRequest(new { message = "Tài khoản không tồn tại." });
        }

        // Kiểm tra mã OTP và thời gian hết hạn
        if (string.IsNullOrEmpty(user.OtpCode) || user.OtpCode != model.OtpCode)
        {
            return BadRequest(new { message = "Mã OTP không chính xác." });
        }

        if (!user.OtpExpiryTime.HasValue || user.OtpExpiryTime.Value < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Mã OTP đã hết hạn, vui lòng đăng nhập lại để nhận mã mới." });
        }

        // Xác thực thành công: hủy mã OTP (set về null)
        user.OtpCode = null;
        user.OtpExpiryTime = null;
        await _userManager.UpdateAsync(user);

        // Lấy danh sách Roles
        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? "Customer";

        // Cấp JWT Token
        var token = await _tokenService.CreateTokenAsync(user, roles);

        // Xác định targetRoute điều hướng theo Role (Mục 3)
        var targetRoute = primaryRole.ToLower() switch
        {
            "admin" => "/admin",
            "staff" => "/staff/dashboard",
            _ => "/home"
        };

        return Ok(new AuthResponseDto
        {
            Token = token,
            Username = user.UserName!,
            Email = user.Email ?? string.Empty,
            Role = primaryRole,
            TargetRoute = targetRoute
        });
    }

    /// <summary>
    /// Chức năng 5.a: Đăng ký / Cập nhật dữ liệu khuôn mặt cho người dùng
    /// POST: /api/auth/register-face
    /// </summary>
    [HttpPost("register-face")]
    public async Task<IActionResult> RegisterFace([FromBody] RegisterFaceDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (model.FaceEmbedding == null || model.FaceEmbedding.Count == 0)
        {
            return BadRequest(new { message = "Vector khuôn mặt không hợp lệ." });
        }

        var user = await _userManager.FindByNameAsync(model.Username)
                   ?? await _userManager.FindByEmailAsync(model.Username);

        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy người dùng với Username: {model.Username}" });
        }

        // Serialize vector đặc trưng sang chuỗi JSON và lưu vào nvarchar(max)
        user.FaceData = System.Text.Json.JsonSerializer.Serialize(model.FaceEmbedding);
        user.FaceRegisteredAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(model.FaceImageUrl))
        {
            user.FaceImageUrl = model.FaceImageUrl;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return StatusCode(500, new { message = "Lỗi khi cập nhật dữ liệu khuôn mặt vào hệ thống.", errors = result.Errors });
        }

        _logger.LogInformation("Đã đăng ký thành công Face ID cho user '{Username}' với vector {Dimension} chiều.",
            user.UserName, model.FaceEmbedding.Count);

        return Ok(new
        {
            success = true,
            message = "Đăng ký khuôn mặt thành công!",
            username = user.UserName,
            registeredAt = user.FaceRegisteredAt,
            vectorDimension = model.FaceEmbedding.Count
        });
    }

    /// <summary>
    /// Chức năng 5.a: Đăng nhập bằng khuôn mặt (Hỗ trợ cả chế độ 1:1 và 1:N)
    /// POST: /api/auth/login-face
    /// Ngưỡng mặc định 0.70 (linh hoạt từ 0.40 đến 0.95 do client truyền)
    /// </summary>
    [HttpPost("login-face")]
    public async Task<IActionResult> LoginFace([FromBody] LoginFaceDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (model.FaceEmbedding == null || model.FaceEmbedding.Count == 0)
        {
            return BadRequest(new { message = "Vector khuôn mặt không được để trống." });
        }

        // Ngưỡng nhận diện linh hoạt: Mặc định 0.70 để tránh False Rejection khi chụp lệch góc/thiếu sáng
        float threshold = model.Threshold is >= 0.40f and <= 0.99f ? model.Threshold.Value : 0.70f;

        FaceMatchResult matchResult;

        if (!string.IsNullOrWhiteSpace(model.Username))
        {
            // Chế độ 1:1 - Xác minh nhanh theo đúng tài khoản
            matchResult = await _faceMatchingService.VerifyFaceAsync(model.Username.Trim(), model.FaceEmbedding, threshold);
        }
        else
        {
            // Chế độ 1:N - Quét tìm tài khoản khớp nhất trong toàn bộ hệ thống
            matchResult = await _faceMatchingService.FindBestMatchAsync(model.FaceEmbedding, threshold);
        }

        if (!matchResult.IsMatch || matchResult.User == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Khuôn mặt không khớp hoặc chưa được đăng ký trong hệ thống.",
                similarityScore = MathF.Round(matchResult.SimilarityScore, 4),
                thresholdUsed = threshold
            });
        }

        var user = matchResult.User;
        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? "Customer";

        // Cấp JWT Token
        var token = await _tokenService.CreateTokenAsync(user, roles);

        // Điều hướng theo Role tương ứng
        var targetRoute = primaryRole.ToLower() switch
        {
            "admin" => "/admin",
            "staff" => "/staff/dashboard",
            _ => "/home"
        };

        _logger.LogInformation("Người dùng '{Username}' đăng nhập thành công bằng Face ID (Score: {Score:F4}, Route: {Route})",
            user.UserName, matchResult.SimilarityScore, targetRoute);

        return Ok(new FaceLoginResponseDto
        {
            Token = token,
            Username = user.UserName!,
            Email = user.Email ?? string.Empty,
            Role = primaryRole,
            TargetRoute = targetRoute,
            SimilarityScore = MathF.Round(matchResult.SimilarityScore, 4)
        });
    }

    /// <summary>
    /// Chức năng 5.a: Kiểm tra trạng thái đã đăng ký khuôn mặt hay chưa
    /// GET: /api/auth/face-status/{username}
    /// </summary>
    [HttpGet("face-status/{username}")]
    public async Task<IActionResult> GetFaceStatus(string username)
    {
        var user = await _userManager.FindByNameAsync(username)
                   ?? await _userManager.FindByEmailAsync(username);

        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy người dùng với Username: {username}" });
        }

        return Ok(new FaceStatusDto
        {
            Username = user.UserName!,
            IsFaceRegistered = !string.IsNullOrEmpty(user.FaceData),
            RegisteredAt = user.FaceRegisteredAt,
            FaceImageUrl = user.FaceImageUrl
        });
    }

    /// <summary>
    /// Chức năng 5.a: Hủy / Xóa dữ liệu khuôn mặt đã đăng ký
    /// DELETE: /api/auth/remove-face/{username}
    /// </summary>
    [HttpDelete("remove-face/{username}")]
    public async Task<IActionResult> RemoveFace(string username)
    {
        var user = await _userManager.FindByNameAsync(username)
                   ?? await _userManager.FindByEmailAsync(username);

        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy người dùng với Username: {username}" });
        }

        user.FaceData = null;
        user.FaceRegisteredAt = null;
        user.FaceImageUrl = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return StatusCode(500, new { message = "Lỗi khi xóa dữ liệu khuôn mặt.", errors = result.Errors });
        }

        return Ok(new
        {
            success = true,
            message = $"Đã xóa thành công dữ liệu khuôn mặt của tài khoản '{user.UserName}'."
        });
    }

    /// <summary>
    /// Endpoint kiểm thử nhanh việc gửi OTP về email thật qua Gmail SMTP
    /// GET: /api/auth/test-send-otp?toEmail=huamoc2005@gmail.com
    /// </summary>
    [HttpGet("test-send-otp")]
    public async Task<IActionResult> TestSendOtp([FromQuery] string? toEmail)
    {
        var targetEmail = string.IsNullOrWhiteSpace(toEmail) ? "huamoc2005@gmail.com" : toEmail.Trim();
        var testOtp = Random.Shared.Next(100000, 999999).ToString();
        try
        {
            await _emailService.SendOtpEmailAsync(targetEmail, testOtp, "Người dùng thử nghiệm");
            return Ok(new
            {
                success = true,
                message = $"Đã gửi mã OTP thử nghiệm ({testOtp}) thành công đến {targetEmail} qua Gmail SMTP!",
                otp = testOtp,
                sentTo = targetEmail
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kiểm thử gửi email tới {Email}", targetEmail);
            return StatusCode(500, new
            {
                success = false,
                message = "Gửi email OTP thất bại.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return email;
        var parts = email.Split('@');
        var name = parts[0];
        var domain = parts[1];
        if (name.Length <= 3)
        {
            return $"{name[0]}***@{domain}";
        }
        return $"{name[..3]}***{name[^2..]}@{domain}";
    }
}

