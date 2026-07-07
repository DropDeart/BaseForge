using System.Globalization;
using Grpc.Core;
using Identity.Grpc;

namespace Orders.Integration;

/// <summary>identity/User servisine senkron (gRPC) erişim sözleşmesi. BaseForge.CodeGen tarafından üretildi.</summary>
public interface IUserClient
{
    /// <summary>Uzak servisten User kaydını getirir; bulunamazsa <see langword="null"/>.</summary>
    Task<UserReference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Uzak User kaydının yerel (zengin) görünümü.</summary>
public sealed class UserReference
{
    /// <summary>Uzak kaydın kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>UserName.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Email.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>FullName.</summary>
    public string FullName { get; set; } = string.Empty;
}

/// <summary><see cref="IUserClient"/>'in Identity servisine gRPC ile bağlanan gerçek implementasyonu.</summary>
public sealed class UserClient(UserService.UserServiceClient client) : IUserClient
{
    public async Task<UserReference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetByIdAsync(
                new UserByIdRequest { Id = id.ToString() },
                cancellationToken: cancellationToken);

            return new UserReference
            {
                Id = Guid.Parse(response.Id),
                UserName = response.UserName,
                Email = response.Email,
                FullName = response.FullName,
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}
