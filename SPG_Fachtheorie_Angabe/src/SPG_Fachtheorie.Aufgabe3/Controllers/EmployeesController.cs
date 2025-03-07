using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SPG_Fachtheorie.Aufgabe3.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<EmployeeDto>> GetAllEmployees() 
        { 

        }
    }
}
