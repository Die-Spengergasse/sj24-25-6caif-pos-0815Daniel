using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using SPG_Fachtheorie.Aufgabe1.Infrastructure;
using SPG_Fachtheorie.Aufgabe1.Model;
using SPG_Fachtheorie.Aufgabe3.Commands;
using SPG_Fachtheorie.Aufgabe3.Dtos;

namespace SPG_Fachtheorie.Aufgabe3.Controllers
{
    [Route("api/[controller]")]  // [controller] bedeutet: das Wort vor Controller
    [ApiController]              // Soll von ASP gemappt werden
    public class EmployeesController : ControllerBase
    {
        private readonly AppointmentContext _db;

        public EmployeesController(AppointmentContext db)
        {
            _db = db;
        }

        /// <summary>
        /// GET /api/employees oder             --> type = null
        /// GET /api/employees?type=Manager     --> type = Manager
        /// GET /api/employees?type=manager     --> type = manager
        /// GET /api/employees?type=            --> type = ""
        /// </summary>
        [HttpGet]
        public ActionResult<List<EmployeeDto>> GetAllEmployees([FromQuery] string? type)
        {
            var employees = _db.Employees
                .Where(e => string.IsNullOrEmpty(type)
                    ? true : e.Type.ToLower() == type.ToLower())
                .Select(e => new EmployeeDto(
                    e.RegistrationNumber, e.FirstName, e.LastName,
                    $"{e.FirstName} {e.LastName}", e.Type))
                .ToList();    //  // [{...}, {...}, ... ]
            return Ok(employees);
        }

        /// <summary>
        /// GET /api/employee/1001
        /// </summary>
        [HttpGet("{registrationNumber}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<EmployeeDetailDto> GetEmployee(int registrationNumber)
        {
            var employees = _db.Employees
                .Where(e => e.RegistrationNumber == registrationNumber)
                .Select(e => new EmployeeDetailDto(
                    e.RegistrationNumber,
                    e.FirstName, e.LastName,
                    $"{e.FirstName} {e.LastName}",
                    e.Address, e.Type))
                .AsNoTracking()
                .FirstOrDefault();  // { .... }
            if (employees is null) { return NotFound(); }
            return Ok(employees);
        }

        ///
        /// POST /api/employee/manager
        [HttpPost("manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult AddManager(NewManagerCommand cmd)
        {
            var manager = new Manager(
                cmd.RegistrationNumber, cmd.FirstName, cmd.LastName,
                cmd.Address is null ? null : new Address(cmd.Address.Street, cmd.Address.Zip, cmd.Address.City),
                cmd.CarType);
            _db.Managers.Add(manager);
            try
            {
                _db.SaveChanges();
            }
            catch (DbUpdateException e)
            {
                // 400 Bad request: clientdaten fehlerhaft, der client soll die Daten nicht erneut senden.
                return Problem(e.InnerException?.Message ?? e.Message, statusCode: 400);
            }
            // Den primary key des neuen DB Objektes zurückgeben.
            return CreatedAtAction(nameof(AddManager), new { manager.RegistrationNumber });
        }

        /// POST /api/employee/manager
        [HttpPost("cashier")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult AddCashier(NewCashierCommand cmd)
        {
            var cashier = new Cashier(
                cmd.RegistrationNumber, cmd.FirstName, cmd.LastName,
                cmd.Address is null ? null : new Address(cmd.Address.Street, cmd.Address.Zip, cmd.Address.City),
                cmd.JobSpezialisation);
            _db.Cashiers.Add(cashier);
            try
            {
                _db.SaveChanges();
            }
            catch (DbUpdateException e)
            {
                // 400 Bad request: clientdaten fehlerhaft, der client soll die Daten nicht erneut senden.
                return Problem(e.InnerException?.Message ?? e.Message, statusCode: 400);
            }
            // Den primary key des neuen DB Objektes zurückgeben.
            return CreatedAtAction(nameof(AddManager), new { cashier.RegistrationNumber });
        }
    }
}
