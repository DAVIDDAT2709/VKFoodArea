using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/pois")]
public class PoiApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public PoiApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.Pois
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                x.PhoneNumber,
                x.ImageUrl,
                x.Latitude,
                x.Longitude,
                x.RadiusMeters,
                x.Description,
                x.TtsScriptVi,
                x.TtsScriptEn,
                x.TtsScriptZh,
                x.TtsScriptJa,
                x.TtsScriptDe,
                x.QrCode,
                x.IsActive
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var poi = await _context.Pois
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                x.PhoneNumber,
                x.ImageUrl,
                x.Latitude,
                x.Longitude,
                x.RadiusMeters,
                x.Description,
                x.TtsScriptVi,
                x.TtsScriptEn,
                x.TtsScriptZh,
                x.TtsScriptJa,
                x.TtsScriptDe,
                x.QrCode,
                x.IsActive
            })
            .FirstOrDefaultAsync();

        if (poi is null)
            return NotFound();

        return Ok(poi);
    }

    [HttpGet("by-qr")]
    public async Task<IActionResult> GetByQr([FromQuery] string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return BadRequest("Missing code.");

        var qrItemMatch = await _context.QrCodeItems
            .AsNoTracking()
            .Include(x => x.Poi)
            .Where(x =>
                x.IsActive &&
                x.Poi != null &&
                x.Poi.IsActive &&
                !string.IsNullOrWhiteSpace(x.Code) &&
                x.Code.ToLower() == normalized)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (qrItemMatch?.Poi is not null)
        {
            var poi = qrItemMatch.Poi;
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
                poi.Description,
                poi.TtsScriptVi,
                poi.TtsScriptEn,
                poi.TtsScriptZh,
                poi.TtsScriptJa,
                poi.TtsScriptDe,
                poi.QrCode,
                poi.IsActive,
                MatchedQrCode = qrItemMatch.Code,
                QrSource = "qr-item"
            });
        }

        var poiMatch = await _context.Pois
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !string.IsNullOrWhiteSpace(x.QrCode) &&
                x.QrCode.ToLower() == normalized)
            .FirstOrDefaultAsync();

        if (poiMatch is null)
            return NotFound();

        return Ok(new
        {
            poiMatch.Id,
            poiMatch.Name,
            poiMatch.Address,
            poiMatch.PhoneNumber,
            poiMatch.ImageUrl,
            poiMatch.Latitude,
            poiMatch.Longitude,
            poiMatch.RadiusMeters,
            poiMatch.Description,
            poiMatch.TtsScriptVi,
            poiMatch.TtsScriptEn,
            poiMatch.TtsScriptZh,
            poiMatch.TtsScriptJa,
            poiMatch.TtsScriptDe,
            poiMatch.QrCode,
            poiMatch.IsActive,
            MatchedQrCode = poiMatch.QrCode,
            QrSource = "poi-default"
        });
    }
}