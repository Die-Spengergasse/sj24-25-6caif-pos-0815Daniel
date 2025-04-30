using Microsoft.EntityFrameworkCore;
using SPG_Fachtheorie.Aufgabe1.Commands;
using SPG_Fachtheorie.Aufgabe1.Exceptions;
using SPG_Fachtheorie.Aufgabe1.Infrastructure;
using SPG_Fachtheorie.Aufgabe1.Model;
using SPG_Fachtheorie.Aufgabe1.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SPG_Fachtheorie.Aufgabe1.Test
{
    [Collection("Sequential")]
    public class PaymentServiceTests
    {
        private AppointmentContext GetEmptyDbContext()
        {
            var options = new DbContextOptionsBuilder<AppointmentContext>()
                .UseSqlite("Data Source=cash.db")
                .Options;

            var db = new AppointmentContext(options);
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            return db;
        }
        public static IEnumerable<object[]> InvalidCreatePaymentCommands =>
            new List<object[]>
            {
                new object[]
                {
                    new NewPaymentCommand
                    {
                        CashDeskNumber = 999,
                        EmployeeRegistrationNumber = 1001,
                        PaymentType = "Cash"
                    },
                    "Invalid cashdesk"
                },
                new object[]
                {
                    new NewPaymentCommand
                    {
                        CashDeskNumber = 1,
                        EmployeeRegistrationNumber = 9999,
                        PaymentType = "Cash"
                    },
                    "Invalid employee"
                },
                new object[]
                {
                    new NewPaymentCommand
                    {
                        CashDeskNumber = 1,
                        EmployeeRegistrationNumber = 2002,
                        PaymentType = "CreditCard"
                    },
                    "Insufficient rights to create a credit card payment."
                }
            };

        [Theory]
        [MemberData(nameof(InvalidCreatePaymentCommands))]
        public void CreatePaymentExceptionsTest(NewPaymentCommand cmd, string expectedMessage)
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            db.CashDesks.Add(new CashDesk(1));
            db.Employees.Add(new Manager(
                1001, "Anna", "Manager", null, 5000m, null, "BMW"));
            db.Employees.Add(new Cashier(
                2002, "Ben", "Cashier", null, 2500m, null, "Kassa 1"));
            db.SaveChanges();

            var service = new PaymentService(db);

            // ACT
            var ex = Assert.Throws<PaymentServiceException>(() => service.CreatePayment(cmd));

            // ASSERT
            Assert.True(ex.Message == expectedMessage);
        }

        [Fact]
        public void CreatePaymentSuccessTest()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            db.CashDesks.Add(new CashDesk(1));
            db.Employees.Add(new Manager(
                1001, "Anna", "Manager", null, 5000m, null, "BMW"));
            db.SaveChanges();

            var service = new PaymentService(db);
            var cmd = new NewPaymentCommand
            {
                CashDeskNumber = 1,
                EmployeeRegistrationNumber = 1001,
                PaymentType = "CreditCard"
            };

            // ACT
            var payment = service.CreatePayment(cmd);

            // ASSERT
            db.ChangeTracker.Clear();
            var paymentFromDb = db.Payments
                .Include(p => p.CashDesk)
                .Include(p => p.Employee)
                .First();

            Assert.True(paymentFromDb.CashDesk.Number == 1);
            Assert.True(paymentFromDb.Employee.RegistrationNumber == 1001);
            Assert.True(paymentFromDb.PaymentType == PaymentType.CreditCard);
            Assert.True(paymentFromDb.Confirmed == null);
        }
    
        [Fact]
        public void ConfirmPayment_NotFound_ThrowsException()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var service = new PaymentService(db);

            // ACT + ASSERT
            var ex = Assert.Throws<PaymentServiceException>(() => service.ConfirmPayment(999));
            Assert.True(ex.IsNotFoundError);
            Assert.True(ex.Message == "Payment not found");
        }

        [Fact]
        public void ConfirmPayment_AlreadyConfirmed_ThrowsException()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var desk = new CashDesk(1);
            var emp = new Cashier(1, "a", "b", null, "x");
            var payment = new Payment(desk, DateTime.UtcNow, emp, PaymentType.Cash)
            {
                Confirmed = DateTime.UtcNow
            };

            db.CashDesks.Add(desk);
            db.Employees.Add(emp);
            db.Payments.Add(payment);
            db.SaveChanges();

            var service = new PaymentService(db);

            // ACT + ASSERT
            var ex = Assert.Throws<PaymentServiceException>(() => service.ConfirmPayment(payment.Id));
            Assert.True(ex.Message == "Payment already confirmed");
        }

        [Fact]
        public void ConfirmPayment_Success()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var desk = new CashDesk(1);
            var emp = new Cashier(1, "a", "b", null, "x");
            var payment = new Payment(desk, DateTime.UtcNow, emp, PaymentType.Cash);

            db.CashDesks.Add(desk);
            db.Employees.Add(emp);
            db.Payments.Add(payment);
            db.SaveChanges();

            var service = new PaymentService(db);

            // ACT
            service.ConfirmPayment(payment.Id);

            // ASSERT
            var confirmed = db.Payments.First(p => p.Id == payment.Id).Confirmed;
            Assert.True(confirmed != null);
        }

        [Fact]
        public void AddPaymentItem_PaymentNotFound_ThrowsException()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var service = new PaymentService(db);
            var cmd = new NewPaymentItemCommand
            {
                PaymentId = 999,
                ArticleName = "Test",
                Amount = 1,
                Price = 1m
            };

            // ACT + ASSERT
            var ex = Assert.Throws<PaymentServiceException>(() => service.AddPaymentItem(cmd));
            Assert.True(ex.Message == "Payment not found.");
        }

        [Fact]
        public void AddPaymentItem_AlreadyConfirmed_ThrowsException()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var desk = new CashDesk(1);
            var emp = new Cashier(1, "a", "b", null, "x");
            var payment = new Payment(desk, DateTime.UtcNow, emp, PaymentType.Cash)
            {
                Confirmed = DateTime.UtcNow
            };

            db.CashDesks.Add(desk);
            db.Employees.Add(emp);
            db.Payments.Add(payment);
            db.SaveChanges();

            var service = new PaymentService(db);
            var cmd = new NewPaymentItemCommand
            {
                PaymentId = payment.Id,
                ArticleName = "Test",
                Amount = 1,
                Price = 1m
            };

            // ACT + ASSERT
            var ex = Assert.Throws<PaymentServiceException>(() => service.AddPaymentItem(cmd));
            Assert.True(ex.Message == "Payment already confirmed.");
        }

        [Fact]
        public void AddPaymentItem_Success()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var desk = new CashDesk(1);
            var emp = new Cashier(1, "a", "b", null, "x");
            var payment = new Payment(desk, DateTime.UtcNow, emp, PaymentType.Cash);

            db.CashDesks.Add(desk);
            db.Employees.Add(emp);
            db.Payments.Add(payment);
            db.SaveChanges();

            var service = new PaymentService(db);
            var cmd = new NewPaymentItemCommand
            {
                PaymentId = payment.Id,
                ArticleName = "Apfel",
                Amount = 2,
                Price = 1.50m
            };

            // ACT
            service.AddPaymentItem(cmd);

            // ASSERT
            var item = db.PaymentItems.First();
            Assert.True(item.ArticleName == "Apfel");
            Assert.True(item.Amount == 2);
        }

        [Fact]
        public void DeletePayment_NotFound_ThrowsException()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var service = new PaymentService(db);

            // ACT + ASSERT
            var ex = Assert.Throws<PaymentServiceException>(() => service.DeletePayment(999, false));
            Assert.True(ex.IsNotFoundError);
        }

        [Fact]
        public void DeletePayment_WithItemsWithoutDeleteFlag_ThrowsException()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var desk = new CashDesk(1);
            var emp = new Cashier(1, "a", "b", null, "x");
            var payment = new Payment(desk, DateTime.UtcNow, emp, PaymentType.Cash);
            var item = new PaymentItem("Banane", 1, 1m, payment);

            db.CashDesks.Add(desk);
            db.Employees.Add(emp);
            db.Payments.Add(payment);
            db.PaymentItems.Add(item);
            db.SaveChanges();

            var service = new PaymentService(db);

            // ACT + ASSERT
            var ex = Assert.Throws<PaymentServiceException>(() => service.DeletePayment(payment.Id, false));
            Assert.True(ex.Message == "Payment has payment items.");
        }

        [Fact]
        public void DeletePayment_WithItemsAndDeleteFlag_Success()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var desk = new CashDesk(1);
            var emp = new Cashier(1, "a", "b", null, "x");
            var payment = new Payment(desk, DateTime.UtcNow, emp, PaymentType.Cash);
            var item = new PaymentItem("Banane", 1, 1m, payment);

            db.CashDesks.Add(desk);
            db.Employees.Add(emp);
            db.Payments.Add(payment);
            db.PaymentItems.Add(item);
            db.SaveChanges();

            var service = new PaymentService(db);

            // ACT
            service.DeletePayment(payment.Id, true);

            // ASSERT
            Assert.True(!db.Payments.Any());
            Assert.True(!db.PaymentItems.Any());
        }

        [Fact]
        public void DeletePayment_WithoutItemsAndDeleteFlagFalse_Success()
        {
            // ARRANGE
            using var db = GetEmptyDbContext();
            var desk = new CashDesk(1);
            var emp = new Cashier(1, "a", "b", null, "x");
            var payment = new Payment(desk, DateTime.UtcNow, emp, PaymentType.Cash);

            db.CashDesks.Add(desk);
            db.Employees.Add(emp);
            db.Payments.Add(payment);
            db.SaveChanges();

            var service = new PaymentService(db);

            // ACT
            service.DeletePayment(payment.Id, false);

            // ASSERT
            Assert.True(!db.Payments.Any());
        }
    }
}
