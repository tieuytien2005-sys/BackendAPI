using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NguyenNhatThanh_2043.Data;
using NguyenNhatThanh_2043.Hubs;
using NguyenNhatThanh_2043.Models;
using NguyenNhatThanh_2043.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình DbContext với SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Cấu hình Identity với ApplicationUser và IdentityRole
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Cấu hình mật khẩu thuận tiện cho việc kiểm thử
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Cấu hình User
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 3. Cấu hình JWT Bearer Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "NguyenNhatThanh_SuperSecretJwtSigningKey_2026_SecureLongKey!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NguyenNhatThanhAuthServer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NguyenNhatThanhClientApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // 1. Cấu hình JWT Token cho SignalR qua Query String (?access_token=...)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && 
               (path.StartsWithSegments("/hubs/location") || path.StartsWithSegments("/hubs/videocall")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// 4. Đăng ký các dịch vụ Dependency Injection (DI)
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IFaceMatchingService, FaceMatchingService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Đăng ký SignalR hỗ trợ cập nhật vị trí thời gian thực
builder.Services.AddSignalR();

builder.Services.AddControllers();

// 5. Cấu hình Swagger / OpenAPI hỗ trợ Bearer Token Authentication
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NguyenNhatThanh_2043 Auth & User Management API",
        Version = "v1",
        Description = "Hệ thống Web API Xác thực 2 bước (OTP), Phân quyền Role và Quản lý Người dùng"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token JWT của bạn vào ô bên dưới (không cần gõ chữ 'Bearer ', chỉ cần dán chuỗi token)."
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

var app = builder.Build();

// 6. Tự động Data Seeding Roles & Admin User khi ứng dụng khởi chạy
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        // Tự động kiểm tra và khởi tạo database nếu chưa có
        await context.Database.EnsureCreatedAsync();

        // Seed Roles (Admin, Staff, Customer) và Admin mặc định
        await DbSeeder.SeedRolesAndAdminAsync(services);
        Console.WriteLine("[DATA SEEDING] Hoàn tất khởi tạo Roles và Tài khoản mặc định!");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi xảy ra trong quá trình Seeding dữ liệu.");
    }
}

// 7. Cấu hình HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NguyenNhatThanh_2043 API v1");
        c.RoutePrefix = string.Empty; // Mở Swagger ngay tại root URL (http://localhost:<port>/)
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LocationHub>("/hubs/location");
app.MapHub<VideoCallHub>("/hubs/videocall");

app.Run();
