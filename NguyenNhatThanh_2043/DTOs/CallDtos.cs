using System.ComponentModel.DataAnnotations;

namespace NguyenNhatThanh_2043.DTOs;

public class CreateCallRoomDto
{
    [Required(ErrorMessage = "Tiêu đề phòng gọi không được để trống")]
    [MaxLength(200, ErrorMessage = "Tiêu đề phòng tối đa 200 ký tự")]
    public string Title { get; set; } = string.Empty;

    [Range(2, 50, ErrorMessage = "Số lượng người tham gia tối đa từ 2 đến 50 người")]
    public int MaxParticipants { get; set; } = 10;
}

public class CallRoomDto
{
    public int Id { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string HostUserId { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public string Status { get; set; } = "Active";
    public int MaxParticipants { get; set; }
    public int ActiveParticipantCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<CallParticipantDto> Participants { get; set; } = new();
}

public class CallParticipantDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public bool IsMuted { get; set; }
    public bool IsCameraOff { get; set; }
    public bool IsOnline { get; set; }
}

public class EndCallResponseDto
{
    public string RoomCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime EndedAt { get; set; }
}

public class ToggleMediaDto
{
    [Required]
    public string RoomCode { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsCameraOff { get; set; }
}
