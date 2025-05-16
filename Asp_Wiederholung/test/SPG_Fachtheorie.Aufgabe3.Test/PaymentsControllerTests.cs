using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SPG_Fachtheorie.Aufgabe3.Dtos;
using Xunit;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SPG_Fachtheorie.Aufgabe3.Test
{
    public class PaymentsControllerTests : IClassFixture<TestWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public PaymentsControllerTests(TestWebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        public static IEnumerable<object[]> GetFilters()
        {
            yield return new object[] { 1, null, 1 }; // cashDesk only
            yield return new object[] { null, new DateTime(2024, 5, 13), 1 }; // dateFrom only
            yield return new object[] { 1, new DateTime(2024, 5, 13), 1 }; // both
        }

        [Theory]
        [MemberData(nameof(GetFilters))]
        public async Task GetAllPayments_ReturnsFilteredResults(int? cashDesk, DateTime? dateFrom, int expectedCount)
        {
            var query = new List<string>();
            if (cashDesk.HasValue) query.Add($"cashDesk={cashDesk.Value}");
            if (dateFrom.HasValue) query.Add($"dateFrom={dateFrom:yyyy-MM-dd}");
            string url = "/api/payments" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

            var result = await _client.GetFromJsonAsync<List<PaymentDto>>(url);

            Assert.NotNull(result);
            Assert.Equal(expectedCount, result.Count);

            if (cashDesk.HasValue)
            {
                Assert.All(result, p => Assert.Equal(cashDesk.Value, p.CashDesk.Number));
            }
            if (dateFrom.HasValue)
            {
                Assert.All(result, p => Assert.True(p.Date >= dateFrom.Value));
            }
        }

        [Fact]
        public async Task GetPaymentById_ReturnsCorrectStatus()
        {
            var responseOk = await _client.GetAsync("/api/payments/1");
            var responseNotFound = await _client.GetAsync("/api/payments/999");
            var responseBadRequest = await _client.GetAsync("/api/payments/abc");

            Assert.Equal(HttpStatusCode.OK, responseOk.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, responseNotFound.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, responseBadRequest.StatusCode);
        }

        [Fact]
        public async Task PatchPayment_ReturnsExpectedStatusCodes()
        {
            var okResponse = await _client.PatchAsJsonAsync("/api/payments/1", new { Confirmed = true });
            var notFoundResponse = await _client.PatchAsJsonAsync("/api/payments/999", new { Confirmed = true });
            var badRequestResponse = await _client.PatchAsJsonAsync("/api/payments/1", new { Confirmed = "invalid" });

            Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, badRequestResponse.StatusCode);
        }

        [Fact]
        public async Task DeletePayment_ReturnsExpectedStatusCodes()
        {
            var deleteOk = await _client.DeleteAsync("/api/payments/1");
            var deleteNotFound = await _client.DeleteAsync("/api/payments/999");
            var deleteBadRequest = await _client.DeleteAsync("/api/payments/abc");

            Assert.Equal(HttpStatusCode.OK, deleteOk.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deleteNotFound.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, deleteBadRequest.StatusCode);
        }
    }
}