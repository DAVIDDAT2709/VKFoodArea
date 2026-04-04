using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/narration-histories")]
public class NarrationHistoryApiController : ControllerBase
{
    private readonly INarrationHistoryService _narrationHistoryService;

    public NarrationHistoryApiController(INarrationHistoryService narrationHistoryService)
    {
        _narrationHistoryService = narrationHistoryService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _narrationHistoryService.GetByIdForApiAsync(id);
        if (item is null) return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NarrationHistoryCreateApiViewModel vm)
    {
        var created = await _narrationHistoryService.CreateFromAppAsync(vm);
        if (created is null)
            return BadRequest("POI không tồn tại hoặc đang bị ẩn.");

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
    }
}
