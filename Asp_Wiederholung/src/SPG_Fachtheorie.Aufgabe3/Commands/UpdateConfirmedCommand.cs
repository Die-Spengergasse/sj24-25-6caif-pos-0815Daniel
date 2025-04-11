using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SPG_Fachtheorie.Aufgabe3.Dtos;

namespace SPG_Fachtheorie.Aufgabe3.Commands
{
    public class UpdateConfirmedCommand
    {
        [Required]
        public string EmployeeRegistrationNumber { get; set; }

        [Required]
        public int CashDeskNumber { get; set; }

        [Required]
        public DateTime PaymentDateTime { get; set; }

        [Required]
        public string PaymentType { get; set; }

        public List<PaymentItemDto>? PaymentItems { get; set; }

        public bool IsPaymentDateTimeValid()
        {
            return PaymentDateTime <= DateTime.Now.AddMinutes(1);
        }
    }
}
