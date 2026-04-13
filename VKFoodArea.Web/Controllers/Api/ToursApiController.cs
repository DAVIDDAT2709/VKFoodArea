using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers.Api;

[ApiController]
[Route("api/tours")]
public class ToursApiController : ControllerBase
{
    private readonly ITourService _tourService;

    public ToursApiController(ITourService tourService)
    {
        _tourService = tourService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tours = await _tourService.GetActiveForApiAsync();
        return Ok(tours);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tour = await _tourService.GetByIdForApiAsync(id);
        return tour is null ? NotFound() : Ok(tour);
    }
}
