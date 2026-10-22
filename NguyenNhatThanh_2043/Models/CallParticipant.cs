using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NguyenNhatThanh_2043.Models;

[Table("CallParticipants")]
public class CallParticipant
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RoomId { get; set; }

    [ForeignKey(nameof(RoomId))]
    public CallRoom? Room { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [MaxLength(100)]
    public string? ConnectionId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeftAt { get; set; }

    public bool IsMuted { get; set; } = false;

    public bool IsCameraOff { get; set; } = false;
}
