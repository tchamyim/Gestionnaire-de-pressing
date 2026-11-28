using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pressings.Services;

namespace Pressings.Controllers;

[Authorize(Policy = StaffPolicies.ActivityLog)]
public sealed class ActivityController(AccessRepository repository) : Controller
{
    public IActionResult Index() => View(repository.GetRecentActivity());
}
