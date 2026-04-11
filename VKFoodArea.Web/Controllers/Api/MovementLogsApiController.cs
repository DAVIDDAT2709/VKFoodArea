using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/movement-logs")]
public class MovementLogsApiController : ControllerBase
{
    private readonly IUserMovementLogService _movementLogService;

    public MovementLogsApiController(IUserMovementLogService movementLogService)
    {
        _movementLogService = movementLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int top = 200)
    {
        var data = await _movementLogService.GetRecentAsync(top);
        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MovementLogCreateApiViewModel vm)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var created = await _movementLogService.CreateFromAppAsync(vm);
        return Ok(created);
    }
}
