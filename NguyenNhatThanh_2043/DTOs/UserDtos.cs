using System.ComponentModel.DataAnnotations;

namespace NguyenNhatThanh_2043.DTOs;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}

public class UpdateUserDto
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [MaxLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? PhoneNumber { get; set; }
}
