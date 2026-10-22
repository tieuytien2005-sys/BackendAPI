using Microsoft.AspNetCore.Identity;
using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = { "Admin", "Staff", "Customer" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Tạo sẵn hoặc cập nhật tài khoản Admin mặc định với email thật để kiểm thử OTP
        var adminEmail = "huamoc2005@gmail.com";
        var adminUser = await userManager.FindByNameAsync("admin") ?? await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
        else if (adminUser.Email != adminEmail)
        {
            adminUser.Email = adminEmail;
            adminUser.NormalizedEmail = adminEmail.ToUpperInvariant();
            await userManager.UpdateAsync(adminUser);
        }
    }
}
