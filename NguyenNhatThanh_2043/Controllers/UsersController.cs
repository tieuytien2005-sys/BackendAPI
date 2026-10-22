using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenNhatThanh_2043.DTOs;
using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <summary>
    /// GET /api/users (Read All): Lấy danh sách tài khoản
    /// Phân quyền: Chỉ cho phép quyền Admin
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Roles = roles
            });
        }

        return Ok(userDtos);
    }

    /// <summary>
    /// GET /api/users/{id} (Read Detail): Xem chi tiết tài khoản
    /// Phân quyền: Yêu cầu đăng nhập bất kỳ role nào
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetUserById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy người dùng với Id: {id}" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto
        {
            Id = user.Id,
            FullName = user.FullName ?? string.Empty,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Roles = roles
        };

        return Ok(userDto);
    }

    /// <summary>
    /// PUT /api/users/{id} (Update): Cập nhật thông tin FullName, PhoneNumber
    /// Phân quyền: Yêu cầu đăng nhập (Admin hoặc chính chủ tài khoản)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        // Kiểm tra quyền: Chỉ Admin hoặc chính chủ mới được sửa thông tin
        if (!isAdmin && currentUserId != id)
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy người dùng với Id: {id}" });
        }

        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Cập nhật thông tin thất bại.", errors = result.Errors });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            message = "Cập nhật thông tin thành công!",
            user = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Roles = roles
            }
        });
    }

    /// <summary>
    /// DELETE /api/users/{id} (Delete): Xóa tài khoản khỏi hệ thống
    /// Phân quyền: Chỉ cho phép quyền Admin
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy người dùng với Id: {id}" });
        }

        // Không cho phép Admin tự xóa chính tài khoản của mình
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == id)
        {
            return BadRequest(new { message = "Bạn không thể tự xóa tài khoản Admin hiện đang đăng nhập." });
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Xóa người dùng thất bại.", errors = result.Errors });
        }

        return Ok(new { message = $"Đã xóa người dùng '{user.UserName}' thành công!" });
    }
}
