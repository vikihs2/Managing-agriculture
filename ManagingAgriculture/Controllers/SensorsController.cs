using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ManagingAgriculture.Services;

namespace ManagingAgriculture.Controllers
{
    [Authorize]
    public class SensorsController : Controller
    {
        private readonly ArduinoService _arduino;

        public SensorsController(ArduinoService arduino)
        {
            _arduino = arduino;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "Soil Humidity Monitor";
            return View();
        }

        [HttpGet]
        public IActionResult GetWaterLevel()
        {
            return Json(new { value = _arduino.GetValue() });
        }
    }
}
