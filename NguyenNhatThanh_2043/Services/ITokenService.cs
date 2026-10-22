using NguyenNhatThanh_2043.Models;

namespace NguyenNhatThanh_2043.Services;

public interface ITokenService
{
    Task<string> CreateTokenAsync(ApplicationUser user, IList<string> roles);
}
