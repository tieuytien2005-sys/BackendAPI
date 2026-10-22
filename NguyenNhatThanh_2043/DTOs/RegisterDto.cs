using System.ComponentModel.DataAnnotations;

namespace NguyenNhatThanh_2043.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [MaxLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [MinLength(3, ErrorMessage = "Tên đăng nhập phải có ít nhất 3 ký tự")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vai trò không được để trống")]
    public string Role { get; set; } = "Customer"; // Admin, Staff, Customer
}
