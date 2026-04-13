using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/resolve-qr")]
public class ResolveQrApiController : ControllerBase
{
    private readonly IQrResolveService _qrResolveService;

    public ResolveQrApiController(IQrResolveService qrResolveService)
    {
        _qrResolveService = qrResolveService;
    }

    [HttpGet]
    public async Task<IActionResult> Resolve([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing code.");

        var result = await _qrResolveService.ResolveAsync(code);
        return result is null ? NotFound() : Ok(result);
    }
}
