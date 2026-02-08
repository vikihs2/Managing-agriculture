using Microsoft.AspNetCore.Mvc;

namespace ManagingAgriculture.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            ViewBag.StatusCode = statusCode;
            
            switch (statusCode)
            {
                case 404:
                    return View("NotFound");
                case 403:
                    return View("Forbidden");
                case 500:
                    return View("ServerError");
                default:
                    return View("Error");
            }
        }

        [Route("/Error")]
        public IActionResult Error()
        {
            return View("Error");
        }

        [Route("/Error/ArduinoDisconnected")]
        public IActionResult ArduinoDisconnected()
        {
            return View("ArduinoDisconnected");
        }
    }
}
