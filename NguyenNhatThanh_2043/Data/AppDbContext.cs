using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserLocationHistory> LocationHistories => Set<UserLocationHistory>();
    public DbSet<CallRoom> CallRooms => Set<CallRoom>();
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customize the ASP.NET Identity model
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.FullName).HasMaxLength(150);
            b.Property(u => u.OtpCode).HasMaxLength(10);
            b.Property(u => u.FaceData).HasColumnType("nvarchar(max)");
            b.Property(u => u.FaceImageUrl).HasColumnType("nvarchar(max)");
            b.Property(u => u.LocationAddress).HasMaxLength(250);

            // Quan hệ 1-N với UserLocationHistory
            b.HasMany(u => u.LocationHistories)
             .WithOne(h => h.User)
             .HasForeignKey(h => h.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng UserLocationHistory
        builder.Entity<UserLocationHistory>(b =>
        {
            b.HasKey(h => h.Id);
            b.Property(h => h.Address).HasMaxLength(250);

            // Đánh index tối ưu truy vấn lịch sử theo UserId và mốc thời gian RecordedAt
            b.HasIndex(h => new { h.UserId, h.RecordedAt });
        });

        // Cấu hình bảng CallRoom
        builder.Entity<CallRoom>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.RoomCode).IsUnique();

            b.HasOne(r => r.HostUser)
             .WithMany()
             .HasForeignKey(r => r.HostUserId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(r => r.Participants)
             .WithOne(p => p.Room)
             .HasForeignKey(p => p.RoomId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng CallParticipant
        builder.Entity<CallParticipant>(b =>
        {
            b.HasKey(p => p.Id);

            b.HasOne(p => p.User)
             .WithMany()
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(p => new { p.RoomId, p.UserId });
        });
    }
}
