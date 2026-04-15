using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/device-presence")]
public class AppDevicePresenceApiController : ControllerBase
{
    private readonly IAppDevicePresenceService _appDevicePresenceService;

    public AppDevicePresenceApiController(IAppDevicePresenceService appDevicePresenceService)
    {
        _appDevicePresenceService = appDevicePresenceService;
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] AppDeviceHeartbeatViewModel vm)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _appDevicePresenceService.UpsertHeartbeatAsync(vm);
        return Ok(new { ok = true });
    }
}