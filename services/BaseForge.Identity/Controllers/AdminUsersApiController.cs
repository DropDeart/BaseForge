using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BaseForge.Identity.Controllers;

/// <summary>Ortak Giriş SPA'sının admin panelindeki kullanıcı/rol yönetimi uçları. Sadece Admin rolüne açık.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = SeedData.AdminRole)]
public sealed class AdminUsersApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>Atanabilir roller: her zaman var olan Admin/User + auth.yaml'daki <c>roles</c> (SeedData ile aynı küme).</summary>
    private readonly string[] _knownRoles;

    public AdminUsersApiController(UserManager<ApplicationUser> userManager, AuthOptions authOptions)
    {
        _userManager = userManager;
        _knownRoles = new[] { SeedData.AdminRole, SeedData.UserRole }.Concat(authOptions.Roles).Distinct(StringComparer.Ordinal).ToArray();
    }

    [HttpGet("roles")]
    public IActionResult Roles() => Ok(_knownRoles);

    [HttpGet("users")]
    public async Task<IActionResult> List()
    {
        var users = _userManager.Users.OrderBy(u => u.Email).ToList();
        var rows = new List<AdminUserRow>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            rows.Add(ToRow(user, roles));
        }

        return Ok(rows);
    }

    [HttpPost("users")]
    public async Task<IActionResult> Add(AddUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new ErrorResponse("E-posta gerekli."));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName,
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse(string.Join(" ", result.Errors.Select(e => e.Description))));
        }

        await _userManager.AddToRoleAsync(user, SeedData.UserRole);
        return Ok(ToRow(user, [SeedData.UserRole]));
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        await _userManager.DeleteAsync(user);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/roles")]
    public async Task<IActionResult> AddRole(Guid id, AddRoleRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (!_knownRoles.Contains(request.Role, StringComparer.Ordinal))
        {
            return BadRequest(new ErrorResponse("Geçersiz rol."));
        }

        if (!await _userManager.IsInRoleAsync(user, request.Role))
        {
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        return Ok();
    }

    [HttpDelete("users/{id:guid}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(Guid id, string role)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        await _userManager.RemoveFromRoleAsync(user, role);
        return NoContent();
    }

    private static AdminUserRow ToRow(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Email ?? user.UserName ?? string.Empty, user.FullName, user.AvatarUrl, user.EmailConfirmed, roles);
}

public sealed record AddUserRequest(string? FullName, string Email);

public sealed record AddRoleRequest(string Role);

public sealed record AdminUserRow(Guid Id, string Email, string? FullName, string? AvatarUrl, bool EmailConfirmed, IEnumerable<string> Roles);
