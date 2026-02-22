using Microsoft.AspNetCore.Mvc;
using ManagingAgriculture.Services;

namespace ManagingAgriculture.Controllers
{
    public class SensorsController : Controller
    {
        private readonly ArduinoService _arduino;

        public SensorsController(ArduinoService arduino)
        {
            _arduino = arduino;
        }

        public IActionResult Index()
        {
            if (!_arduino.IsConnected())
            {
                return View("Disconnected");
            }
            return View();
        }

        [HttpGet]
        public IActionResult GetWaterLevel()
        {
            return Json(new { value = _arduino.GetValue() });
        }
    }
}
