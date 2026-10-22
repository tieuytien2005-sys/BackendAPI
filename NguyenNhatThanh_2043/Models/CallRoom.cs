using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NguyenNhatThanh_2043.Models;

[Table("CallRooms")]
public class CallRoom
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string RoomCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string HostUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(HostUserId))]
    public ApplicationUser? HostUser { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Ended

    public int MaxParticipants { get; set; } = 10;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public ICollection<CallParticipant> Participants { get; set; } = new List<CallParticipant>();
}
