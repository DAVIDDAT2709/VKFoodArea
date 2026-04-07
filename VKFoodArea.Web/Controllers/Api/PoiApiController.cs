using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Dtos;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/pois")]
public class PoiApiController : ControllerBase
{
    private readonly IPoiService _poiService;

    public PoiApiController(IPoiService poiService)
    {
        _poiService = poiService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _poiService.GetActiveForApiAsync();
        return Ok(data.Select(MapBaseResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var poi = await _poiService.GetByIdForApiAsync(id);
        if (poi is null)
            return NotFound();

        return Ok(MapBaseResponse(poi));
    }

    [HttpGet("by-qr")]
    public async Task<IActionResult> GetByQr([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing code.");

        var poi = await _poiService.GetByQrCodeForApiAsync(code);
        if (poi is null)
            return NotFound();

        return Ok(new
        {
            poi.Id,
            poi.Name,
            poi.Address,
            poi.PhoneNumber,
            poi.ImageUrl,
            poi.Latitude,
            poi.Longitude,
            poi.RadiusMeters,
            poi.Priority,
            poi.Description,
            poi.TtsScriptVi,
            poi.TtsScriptEn,
            poi.TtsScriptZh,
            poi.TtsScriptJa,
            poi.TtsScriptDe,
            poi.QrCode,
            poi.IsActive,
            poi.MatchedQrCode,
            poi.QrSource
        });
    }

    private static object MapBaseResponse(PoiDto poi) => new
    {
        poi.Id,
        poi.Name,
        poi.Address,
        poi.PhoneNumber,
        poi.ImageUrl,
        poi.Latitude,
        poi.Longitude,
        poi.RadiusMeters,
        poi.Priority,
        poi.Description,
        poi.TtsScriptVi,
        poi.TtsScriptEn,
        poi.TtsScriptZh,
        poi.TtsScriptJa,
        poi.TtsScriptDe,
        poi.QrCode,
        poi.IsActive
    };
}
