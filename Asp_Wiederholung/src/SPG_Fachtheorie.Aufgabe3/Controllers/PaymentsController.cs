﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPG_Fachtheorie.Aufgabe1.Infrastructure;
using SPG_Fachtheorie.Aufgabe1.Model;
using SPG_Fachtheorie.Aufgabe3.Dtos;

namespace SPG_Fachtheorie.Aufgabe3.Controllers
{
    [Route("api/[controller]")]  // --> api/payments
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly AppointmentContext _db;

        public PaymentsController(AppointmentContext db)
        {
            _db = db;
        }

        /// <summary>
        /// GET /api/payments
        /// GET /api/payments?cashDesk=1
        /// GET /api/payments?dateFrom=2024-05-13
        /// GET /api/payments?dateFrom=2024-05-13&cashDesk=1
        /// </summary>
        [HttpGet]
        public ActionResult<List<PaymentDto>> GetAllPayments(
            [FromQuery] int? cashDesk,
            [FromQuery] DateTime? dateFrom)
        {
            var payments = _db.Payments
                .Where(p => (!cashDesk.HasValue || p.CashDesk.Number == cashDesk.Value)
                         && (!dateFrom.HasValue || p.PaymentDateTime >= dateFrom.Value))
                .Select(p => new PaymentDto(
                    p.Id, p.Employee.FirstName, p.Employee.LastName,
                    p.PaymentDateTime,
                    p.CashDesk.Number, p.PaymentType.ToString(),
                    p.PaymentItems.Sum(pi => pi.Price)))
                .ToList();
            return Ok(payments);
        }

        /// <summary>
        /// GET /api/payments/{id}
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public ActionResult<PaymentDetailDto> GetPaymentById(int id)
        {
            var payment = _db.Payments
                .Where(p => p.Id == id)
                .Select(p => new PaymentDetailDto(
                    p.Id, p.Employee.FirstName, p.Employee.LastName,
                    p.CashDesk.Number, p.PaymentType.ToString(),
                    p.PaymentItems
                        .Select(pi => new PaymentItemDto(
                            pi.ArticleName, pi.Amount, pi.Price))
                        .ToList()))
                .FirstOrDefault();
            if (payment is null) return NotFound();
            return Ok(payment);
        }

        [HttpPost]
        public ActionResult CreatePayment([FromBody] NewPaymentCommand command)
        {
            // Validate payment date
            if (!command.IsPaymentDateTimeValid())
            {
                return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid payment date",
                    detail: "Payment date cannot be more than 1 minute in the future."));
            }

            try
            {
                // Find the cash desk by number
                var cashDesk = _db.CashDesks.FirstOrDefault(c => c.Number == command.CashDeskNumber);
                if (cashDesk == null)
                {
                    return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                        HttpContext,
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid cash desk",
                        detail: $"Cash desk with number {command.CashDeskNumber} not found."));
                }

                // Find the employee by registration number
                var employee = _db.Employees.FirstOrDefault(e =>
                    e.RegistrationNumber == command.EmployeeRegistrationNumber);
                if (employee == null)
                {
                    return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                        HttpContext,
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid employee",
                        detail: $"Employee with registration number {command.EmployeeRegistrationNumber} not found."));
                }

                // Parse payment type from string to enum
                if (!Enum.TryParse<PaymentType>(command.PaymentType, true, out var paymentType))
                {
                    return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                        HttpContext,
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid payment type",
                        detail: $"Payment type '{command.PaymentType}' is not valid. Valid values are: {string.Join(", ", Enum.GetNames<PaymentType>())}"));
                }

                // Create new payment
                var payment = new Payment(cashDesk, command.PaymentDateTime, employee, paymentType);

                // Save to database
                _db.Payments.Add(payment);
                _db.SaveChanges();

                // Return 201 Created with payment ID
                return CreatedAtAction(nameof(GetPaymentById), new { id = payment.Id }, payment.Id);
            }
            catch (Exception ex)
            {
                return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Error creating payment",
                    detail: ex.Message));
            }
        }

        [HttpDelete("{id}")]
        public ActionResult DeletePayment(int id, [FromQuery] bool deleteItems = false)
        {
            try
            {
                // Find the payment
                var payment = _db.Payments
                    .Include(p => p.PaymentItems)
                    .FirstOrDefault(p => p.Id == id);

                if (payment == null)
                {
                    return NotFound(ProblemDetailsFactory.CreateProblemDetails(
                        HttpContext,
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Payment not found",
                        detail: $"Payment with ID {id} could not be found."));
                }

                // Check if payment has items and deleteItems is false
                if (!deleteItems && payment.PaymentItems.Any())
                {
                    return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                        HttpContext,
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Cannot delete payment",
                        detail: "Payment has payment items."));
                }

                // If deleteItems is true, remove all payment items first
                if (deleteItems && payment.PaymentItems.Any())
                {
                    _db.PaymentItems.RemoveRange(payment.PaymentItems);
                }

                // Remove the payment
                _db.Payments.Remove(payment);
                _db.SaveChanges();

                // Return 204 No Content for successful deletion
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Error deleting payment",
                    detail: ex.Message));
            }
        }
    }
}
