using System.Net;
using FrontendNet.Models;
using FrontendNet.Tests.Support;

namespace FrontendNet.Tests.Integration;

public class BuyerFlowsTests
{
    [Fact]
    public async Task StoreCatalog_SupportsSearchEncodingFilterSortAndPagination()
    {
        await using var factory = new FrontendWebApplicationFactory();
        // Add extra products so pagination (PageSize=5) spans 2 pages
        var sellerId = factory.Backend.UsersByEmail["seller@uv.mx"].Id;
        for (var i = 10; i <= 14; i++)
        {
            factory.Backend.Products[i] = new Product
            {
                ProductId = i,
                SellerId = sellerId,
                Title = $"Gadget #{i} & Pro",
                Description = "Item extra",
                Price = 100m + i,
                Stock = 5,
                ImagePublicId = $"products/gadget-{i}",
                ImageUrl = $"https://res.cloudinary.com/demo/image/upload/v1/gadget-{i}.jpg",
                Categories = [factory.Backend.Categories[2]]
            };
        }

        using var client = factory.CreateBrowserClient();
        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#1234");

        // Buyer must NOT see imageless draft product #2 (Mouse Inalámbrico) in list or detail
        var listResp = await client.GetAsync("/Shopping?s=Mouse");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var listHtml = FrontendWebApplicationFactory.DecodeHtml(await listResp.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Mouse Inalámbrico", listHtml);
        var draftDetailResp = await client.GetAsync("/Shopping/Details/2");
        Assert.Equal(HttpStatusCode.NotFound, draftDetailResp.StatusCode);

        var searchResp = await client.GetAsync("/Shopping?s=Gadget+%2310+%26+Pro");
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        var searchHtml = FrontendWebApplicationFactory.DecodeHtml(await searchResp.Content.ReadAsStringAsync());
        Assert.Contains("Gadget #10 & Pro", searchHtml);

        var page2Resp = await client.GetAsync("/Shopping?order=price_desc&page=2");
        Assert.Equal(HttpStatusCode.OK, page2Resp.StatusCode);
        var page2Html = FrontendWebApplicationFactory.DecodeHtml(await page2Resp.Content.ReadAsStringAsync());
        Assert.Contains("Página 2 de 2", page2Html);

        var detailResp = await client.GetAsync("/Shopping/Details/1");
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
        var detailHtml = FrontendWebApplicationFactory.DecodeHtml(await detailResp.Content.ReadAsStringAsync());
        Assert.Contains("https://res.cloudinary.com/demo/image/upload/v1/keyboard.jpg", detailHtml);
        Assert.Contains("Disponible (10 unidades)", detailHtml);
    }

    [Fact]
    public async Task CartAddressesCheckoutMultiSellerAndCancelFlow_WorksEndToEnd()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#1234");

        // Add products from 2 different sellers
        var add1 = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Shopping/Details/1",
            "/ShoppingCart/Add",
            [new("productId", "1"), new("quantity", "2")]);
        Assert.Equal(HttpStatusCode.Redirect, add1.StatusCode);

        var add3 = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Shopping/Details/3",
            "/ShoppingCart/Add",
            [new("productId", "3"), new("quantity", "1")]);
        Assert.Equal(HttpStatusCode.Redirect, add3.StatusCode);

        // Update quantity
        var update = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/ShoppingCart",
            "/ShoppingCart/Update",
            [new("productId", "1"), new("quantity", "1")]);
        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);

        var cartHtml = FrontendWebApplicationFactory.DecodeHtml(await (await client.GetAsync("/ShoppingCart")).Content.ReadAsStringAsync());
        Assert.Contains("Teclado Mecánico", cartHtml);
        Assert.Contains("Monitor 27 Pulgadas", cartHtml);

        // Create a new shipping address
        var createAddr = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Addresses/Create",
            "/Addresses/Create",
            [
                new("Label", "Trabajo"),
                new("RecipientName", "Comprador Demo"),
                new("Street", "Paseo de la Reforma"),
                new("ExteriorNumber", "250"),
                new("Neighborhood", "Juárez"),
                new("City", "CDMX"),
                new("State", "Ciudad de México"),
                new("PostalCode", "06600"),
                new("Country", "MX"),
                new("IsDefault", "true")
            ]);
        Assert.Equal(HttpStatusCode.Redirect, createAddr.StatusCode);

        // Checkout page loads quote automatically
        var checkoutResp = await client.GetAsync("/Orders/Checkout");
        Assert.Equal(HttpStatusCode.OK, checkoutResp.StatusCode);
        var checkoutHtml = FrontendWebApplicationFactory.DecodeHtml(await checkoutResp.Content.ReadAsStringAsync());
        Assert.Contains("2. Cotización del pedido", checkoutHtml);
        Assert.Contains("Paseo de la Reforma", checkoutHtml);

        // Simulate 409 conflict on first confirm attempt -> redirects back to Checkout with friendly message
        factory.Backend.SimulateCheckoutConflict = true;
        var quoteToken = factory.Backend.QuotesByToken.Keys.Last();
        var conflictResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Orders/Checkout",
            "/Orders/ConfirmCheckout",
            [new("quote", quoteToken), new("addressId", "2")]);
        Assert.Equal(HttpStatusCode.Redirect, conflictResp.StatusCode);
        Assert.Contains("/Orders/Checkout", conflictResp.Headers.Location?.OriginalString);

        // Confirm on second attempt -> creates 2 orders (one per seller)
        var freshCheckoutHtml = await (await client.GetAsync("/Orders/Checkout")).Content.ReadAsStringAsync();
        Assert.Contains("Cambió el precio", FrontendWebApplicationFactory.DecodeHtml(freshCheckoutHtml));

        var latestQuote = factory.Backend.QuotesByToken.Keys.Last();
        var confirmResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Orders/Checkout",
            "/Orders/ConfirmCheckout",
            [new("quote", latestQuote), new("addressId", "2")]);
        Assert.Equal(HttpStatusCode.Redirect, confirmResp.StatusCode);
        Assert.Equal("/Orders", confirmResp.Headers.Location?.OriginalString);

        Assert.Equal(2, factory.Backend.Orders.Count);
        var firstOrder = factory.Backend.Orders[0];

        // Buyer cancels first order with reason
        var cancelResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            $"/Orders/Details/{firstOrder.Id}",
            $"/Orders/Cancel/{firstOrder.Id}",
            [new("reason", "Ya no lo necesito")]);
        Assert.Equal(HttpStatusCode.Redirect, cancelResp.StatusCode);

        var orderDetailHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync($"/Orders/Details/{firstOrder.Id}")).Content.ReadAsStringAsync());
        Assert.Contains("Cancelado", orderDetailHtml);
        Assert.Contains("Ya no lo necesito", orderDetailHtml);
    }

    [Fact]
    public async Task Buyer_IsBlockedFromAdminAndSellerRoutes()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#1234");

        foreach (var path in new[] { "/Categories", "/Users", "/AuditLog", "/Products/Create" })
        {
            var resp = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            Assert.Contains("/Home/AccessDenied", resp.Headers.Location?.OriginalString);
        }
    }
}
