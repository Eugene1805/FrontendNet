using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FrontendNet.Helpers;
using FrontendNet.Models;
using FrontendNet.Tests.Support;

namespace FrontendNet.Tests.Unit;

public class HelpersAndModelsTests
{
    [Fact]
    public void JwtInfo_ReadsClaimsWithoutValidatingSignature()
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(5);
        var jwt = FakeBackendState.CreateJwt("user-1", "sess-99", exp, "issuer-a", "aud-b");

        var info = JwtInfo.Read(jwt);

        Assert.NotNull(info);
        Assert.Equal("sess-99", info.SessionId);
        Assert.Equal("user-1", info.Subject);
        Assert.Equal("issuer-a", info.Issuer);
        Assert.Equal("aud-b", info.Audience);
        Assert.Equal(exp.ToUnixTimeSeconds(), info.ExpiresUtc!.Value.ToUnixTimeSeconds());
        Assert.Null(JwtInfo.Read("not-a-jwt"));
    }

    [Fact]
    public void DisplayFormatter_FormatsLabelsCurrencyAndDatesSafely()
    {
        Assert.Equal("Administrador", DisplayFormatter.ProfileLabel(Roles.Admin));
        Assert.Equal("Vendedor", DisplayFormatter.ProfileLabel(Roles.Seller));
        Assert.Equal("Comprador", DisplayFormatter.ProfileLabel(Roles.Buyer));

        Assert.Equal("En preparación", DisplayFormatter.OrderStatusLabel(OrderStatus.Preparing));
        Assert.Equal("Cancelado", DisplayFormatter.OrderStatusLabel(OrderStatus.Canceled));
        Assert.Equal("Pendiente de aprobación", DisplayFormatter.SellerStatusLabel(SellerStatuses.Pending));
        Assert.Equal("Éxito", DisplayFormatter.AuditOutcomeLabel("SUCCESS"));

        Assert.Equal(1250.50m, DisplayFormatter.ParseDecimal("1250.50"));
        Assert.Contains("1,250.50", DisplayFormatter.FormatCurrency("1250.50"));

        Assert.Equal("—", DisplayFormatter.FormatLocalDateTime((DateTime?)null));
        var formatted = DisplayFormatter.FormatLocalDateTime(new DateTime(2026, 11, 18, 18, 0, 0, DateTimeKind.Utc));
        Assert.NotEqual("—", formatted);
    }

    [Fact]
    public void OrderStatus_EnforcesStaffAndBuyerTransitions()
    {
        Assert.Equal([OrderStatus.Preparing, OrderStatus.Canceled], OrderStatus.GetNextStatusesForStaff(OrderStatus.Pending));
        Assert.Equal([OrderStatus.Shipped, OrderStatus.Canceled], OrderStatus.GetNextStatusesForStaff(OrderStatus.Preparing));
        Assert.Equal([OrderStatus.Delivered], OrderStatus.GetNextStatusesForStaff(OrderStatus.Shipped));
        Assert.Empty(OrderStatus.GetNextStatusesForStaff(OrderStatus.Delivered));
        Assert.Empty(OrderStatus.GetNextStatusesForStaff(OrderStatus.Canceled));

        Assert.True(OrderStatus.CanBuyerCancel(OrderStatus.Pending));
        Assert.False(OrderStatus.CanBuyerCancel(OrderStatus.Preparing));
        Assert.False(OrderStatus.CanBuyerCancel(OrderStatus.Shipped));
    }

    [Fact]
    public void PagedList_AndUrlHelper_WorkAsExpected()
    {
        var paged = new PagedList<int>(Enumerable.Range(1, 12), page: 2, pageSize: 5);
        Assert.Equal(2, paged.Page);
        Assert.Equal(3, paged.TotalPages);
        Assert.Equal(12, paged.Total);
        Assert.Equal([6, 7, 8, 9, 10], paged.Items);

        Assert.Equal("a%26b%23c%2Bd", UrlHelper.Encode("a&b#c+d"));
    }

    [Fact]
    public void NumericBooleanConverter_HandlesBooleansNumbersAndStrings()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var fromNum = JsonSerializer.Deserialize<Category>("""{"categoryId":1,"name":"Tech","protected":1}""", opts);
        var fromBool = JsonSerializer.Deserialize<Category>("""{"categoryId":2,"name":"Misc","protected":true}""", opts);
        var fromZero = JsonSerializer.Deserialize<Category>("""{"categoryId":3,"name":"Free","protected":0}""", opts);

        Assert.True(fromNum!.Protected);
        Assert.True(fromBool!.Protected);
        Assert.False(fromZero!.Protected);
    }

    [Fact]
    public void ProductFormModel_ValidatesMandatoryNonEmptyAndMaxBytesDescription()
    {
        var whitespaceDesc = new ProductFormModel
        {
            Title = "Teclado",
            Description = "   ",
            Price = 100.00m,
            Stock = 1
        };
        var resultsEmpty = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(whitespaceDesc, new ValidationContext(whitespaceDesc), resultsEmpty, true));

        // 22000 3-byte chars = 66000 UTF-8 bytes (> 65535B) while UTF-16 length <= 65535
        var overBytesDesc = new ProductFormModel
        {
            Title = "Teclado",
            Description = new string('€', 22000),
            Price = 100.00m,
            Stock = 1
        };
        var resultsOverBytes = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(overBytesDesc, new ValidationContext(overBytesDesc), resultsOverBytes, true));

        var validModel = new ProductFormModel
        {
            Title = "Teclado",
            Description = "Descripción válida",
            Price = 100.00m,
            Stock = 1
        };
        var resultsValid = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(validModel, new ValidationContext(validModel), resultsValid, true));
    }
}
