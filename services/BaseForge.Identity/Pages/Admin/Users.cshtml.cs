using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Pages.Admin;

[Authorize(Roles = SeedData.AdminRole)]
public sealed class UsersModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public List<UserRow> Users { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        var rows = new List<UserRow>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            rows.Add(new UserRow(user.Email ?? user.UserName ?? "-", user.FullName, user.EmailConfirmed, roles));
        }

        Users = rows;
    }

    public sealed record UserRow(string Email, string? FullName, bool EmailConfirmed, IList<string> Roles);
}
