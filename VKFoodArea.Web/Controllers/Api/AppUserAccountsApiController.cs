using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/app-users")]
public class AppUserAccountsApiController : ControllerBase
{
    private readonly IAppUserAccountService _appUserAccountService;

    public AppUserAccountsApiController(IAppUserAccountService appUserAccountService)
    {
        _appUserAccountService = appUserAccountService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] AppUserAccountSyncViewModel vm)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _appUserAccountService.SyncFromAppAsync(vm);
        return Ok(user);
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] string? userKey)
    {
        var status = await _appUserAccountService.GetStatusAsync(userKey);
        return Ok(status);
    }
}
