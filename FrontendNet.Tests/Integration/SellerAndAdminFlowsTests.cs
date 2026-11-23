using System.Net;
using System.Net.Http.Headers;
using FrontendNet.Models;
using FrontendNet.Tests.Support;

namespace FrontendNet.Tests.Integration;

public class SellerAndAdminFlowsTests
{
    [Fact]
    public async Task UnapprovedSeller_SeesPendingNoticeOnProductsAndOrders()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "pending-seller@uv.mx", "Seller#1234");

        var productsHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Products")).Content.ReadAsStringAsync());
        Assert.Contains("Cuenta de vendedor en revisión", productsHtml);
        Assert.Contains("Pendiente de aprobación", productsHtml);

        var ordersHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Orders")).Content.ReadAsStringAsync());
        Assert.Contains("Cuenta de vendedor en revisión", ordersHtml);
    }

    [Fact]
    public async Task ApprovedSeller_ManagesOwnProductsImagesCategoriesAndOrderTransitions()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "seller@uv.mx", "Seller#1234");

        // Own products only (does not show Monitor 27 Pulgadas owned by other-seller@uv.mx)
        var indexHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Products")).Content.ReadAsStringAsync());
        Assert.Contains("Teclado Mecánico", indexHtml);
        Assert.DoesNotContain("Monitor 27 Pulgadas", indexHtml);

        // Cannot edit another seller's product
        var otherEdit = await client.GetAsync("/Products/Edit/3");
        Assert.Equal(HttpStatusCode.Redirect, otherEdit.StatusCode);
        Assert.Contains("/Home/AccessDenied", otherEdit.Headers.Location?.OriginalString);

        // Create product with image upload failure -> product preserved & redirected to Edit
        factory.Backend.SimulateImageUploadError = (HttpStatusCode.ServiceUnavailable, "Cloudinary no configurado.");
        var token = await factory.ExtractAntiforgeryTokenAsync(client, "/Products/Create");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        form.Add(new StringContent("Webcam HD"), "Title");
        form.Add(new StringContent("Cámara 1080p"), "Description");
        form.Add(new StringContent("799.00"), "Price");
        form.Add(new StringContent("12"), "Stock");
        form.Add(new StringContent("1"), "CategoryId");
        var imgContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1, 2]);
        imgContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(imgContent, "Image", "webcam.jpg");

        var createResp = await client.PostAsync("/Products/Create", form);
        Assert.Equal(HttpStatusCode.Redirect, createResp.StatusCode);
        Assert.Contains("/Products/Edit/", createResp.Headers.Location?.OriginalString);

        var createdId = factory.Backend.Products.Keys.Max();
        var editRedirectHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync($"/Products/Edit/{createdId}")).Content.ReadAsStringAsync());
        Assert.Contains("no se pudo subir la imagen", editRedirectHtml);

        // Retry image upload once Cloudinary is available
        factory.Backend.SimulateImageUploadError = null;
        var editToken = await factory.ExtractAntiforgeryTokenAsync(client, $"/Products/Edit/{createdId}");
        using var uploadForm = new MultipartFormDataContent();
        uploadForm.Add(new StringContent(editToken), "__RequestVerificationToken");
        var retryImg = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1, 2]);
        retryImg.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        uploadForm.Add(retryImg, "image", "webcam.jpg");

        var uploadResp = await client.PostAsync($"/Products/UploadImage/{createdId}", uploadForm);
        Assert.Equal(HttpStatusCode.Redirect, uploadResp.StatusCode);
        Assert.NotNull(factory.Backend.Products[createdId].ImageUrl);

        // Seed an order for seller@uv.mx and advance its status
        var sellerOrder = new Order
        {
            Id = "900",
            BuyerId = factory.Backend.UsersByEmail["buyer@uv.mx"].Id,
            SellerId = factory.Backend.UsersByEmail["seller@uv.mx"].Id,
            Status = OrderStatus.Pending,
            Total = "1250.00",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = [new OrderItem { Id = "1", OrderId = "900", ProductId = 1, ProductTitle = "Teclado Mecánico", Quantity = 1, UnitPrice = "1250.00" }]
        };
        factory.Backend.Orders.Add(sellerOrder);

        var prepResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Orders/Details/900",
            "/Orders/UpdateStatus/900",
            [new("status", OrderStatus.Preparing)]);
        Assert.Equal(HttpStatusCode.Redirect, prepResp.StatusCode);
        Assert.Equal(OrderStatus.Preparing, factory.Backend.Orders.First(o => o.Id == "900").Status);
    }

    [Fact]
    public async Task Admin_ManagesCategoriesUsersSellerModerationAndAuditLogs()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "admin@uv.mx", "Admin#1234");

        // Admin sees all products read-only (no Create link, explicit seller-account notice)
        // and cannot open product mutation routes (/Products/Create, Edit, Delete, Categories)
        var productsHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Products")).Content.ReadAsStringAsync());
        Assert.Contains("Teclado Mecánico", productsHtml);
        Assert.Contains("Monitor 27 Pulgadas", productsHtml);
        Assert.DoesNotContain("Crear nuevo", productsHtml);
        Assert.Contains(
            "Los administradores no publican productos; para publicar o editar artículos se requiere una cuenta de vendedor aprobada",
            productsHtml);

        foreach (var restrictedPath in new[]
                 {
                     "/Products/Create",
                     "/Products/Edit/1",
                     "/Products/Delete/1",
                     "/Products/Categories/1"
                 })
        {
            var deniedResp = await client.GetAsync(restrictedPath);
            Assert.Equal(HttpStatusCode.Redirect, deniedResp.StatusCode);
            Assert.Contains("/Home/AccessDenied", deniedResp.Headers.Location?.OriginalString);
        }

        // Protected category delete shows backend business message
        var deleteProtected = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Categories/Delete/1",
            "/Categories/Delete/1",
            []);
        Assert.Equal(HttpStatusCode.Redirect, deleteProtected.StatusCode);
        var deleteProtectedHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync(deleteProtected.Headers.Location!.OriginalString)).Content.ReadAsStringAsync());
        Assert.Contains("categorías protegidas", deleteProtectedHtml);

        // Create and delete non-protected category
        var createCat = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Categories/Create",
            "/Categories/Create",
            [new("Name", "Audio")]);
        Assert.Equal(HttpStatusCode.Redirect, createCat.StatusCode);
        Assert.Contains(factory.Backend.Categories.Values, c => c.Name == "Audio");

        // Users index has NO "Crear nuevo" link and allows approving pending seller
        var usersHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Users")).Content.ReadAsStringAsync());
        Assert.DoesNotContain("Crear nuevo", usersHtml);
        Assert.Contains("pending-seller@uv.mx", usersHtml);

        var pendingSeller = factory.Backend.UsersByEmail["pending-seller@uv.mx"];
        var approveResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Users/Details?email=pending-seller%40uv.mx",
            "/Users/ApproveSeller",
            [
                new("userId", pendingSeller.Id),
                new("email", pendingSeller.Email)
            ]);
        Assert.Equal(HttpStatusCode.Redirect, approveResp.StatusCode);
        Assert.Equal(SellerStatuses.Approved, pendingSeller.SellerStatus);

        // Suspend approved seller with reason
        var suspendResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Users/Details?email=pending-seller%40uv.mx",
            "/Users/SuspendSeller",
            [
                new("userId", pendingSeller.Id),
                new("email", pendingSeller.Email),
                new("reason", "Incumplimiento de catálogo")
            ]);
        Assert.Equal(HttpStatusCode.Redirect, suspendResp.StatusCode);
        Assert.Equal(SellerStatuses.Suspended, pendingSeller.SellerStatus);

        // Reject another pending seller with reason
        factory.Backend.UsersByEmail["reject-me@uv.mx"] = new FakeUserRecord
        {
            Id = "55555555-5555-5555-5555-555555555555",
            Email = "reject-me@uv.mx",
            Name = "Vendedor Rechazable",
            Password = "Seller#1234",
            Profile = Roles.Seller,
            Status = UserStatuses.Active,
            SellerStatus = SellerStatuses.Pending,
            StoreName = "Tienda Temporal"
        };
        var rejectResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Users/Details?email=reject-me%40uv.mx",
            "/Users/RejectSeller",
            [
                new("userId", "55555555-5555-5555-5555-555555555555"),
                new("email", "reject-me@uv.mx"),
                new("rejectionReason", "Documentación incompleta")
            ]);
        Assert.Equal(HttpStatusCode.Redirect, rejectResp.StatusCode);
        Assert.Equal(SellerStatuses.Rejected, factory.Backend.UsersByEmail["reject-me@uv.mx"].SellerStatus);

        // Admin edits and deletes non-admin user, while admin account delete is blocked
        var editUserResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Users/Edit?email=reject-me%40uv.mx",
            "/Users/Edit",
            [
                new("Id", "55555555-5555-5555-5555-555555555555"),
                new("Email", "reject-me@uv.mx"),
                new("Profile", Roles.Seller),
                new("Name", "Vendedor Editado"),
                new("Status", UserStatuses.Suspended)
            ]);
        Assert.Equal(HttpStatusCode.Redirect, editUserResp.StatusCode);
        Assert.Equal("Vendedor Editado", factory.Backend.UsersByEmail["reject-me@uv.mx"].Name);
        Assert.Equal(UserStatuses.Suspended, factory.Backend.UsersByEmail["reject-me@uv.mx"].Status);

        var deleteUserResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Users/Delete?email=reject-me%40uv.mx",
            "/Users/Delete?email=reject-me%40uv.mx",
            []);
        Assert.Equal(HttpStatusCode.Redirect, deleteUserResp.StatusCode);
        Assert.True(factory.Backend.UsersByEmail["reject-me@uv.mx"].Deleted);

        var deleteSelfHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Users/Delete?email=admin%40uv.mx")).Content.ReadAsStringAsync());
        Assert.Contains("está protegida y no puede eliminarse", deleteSelfHtml);

        // Audit log renders entries and cursor pagination works
        var auditResp = await client.GetAsync("/AuditLog?limit=10");
        Assert.Equal(HttpStatusCode.OK, auditResp.StatusCode);
        var auditHtml = FrontendWebApplicationFactory.DecodeHtml(await auditResp.Content.ReadAsStringAsync());
        Assert.Contains("Mostrando 10 elementos", auditHtml);
        Assert.Contains("before=", auditHtml);
    }

    [Fact]
    public async Task NetworkOutage_ReturnsFriendly503PageWithoutTechnicalDetails()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "admin@uv.mx", "Admin#1234");
        factory.Backend.SimulateNetworkFailure = true;

        var resp = await client.GetAsync("/Categories");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        var html = FrontendWebApplicationFactory.DecodeHtml(await resp.Content.ReadAsStringAsync());
        Assert.Contains("No fue posible comunicarse con el servidor", html);
        Assert.DoesNotContain("Simulated network outage", html);
    }

    [Fact]
    public async Task ApprovedSeller_CreateProduct_RejectsZeroPriceZeroStock_AndStockOverflowShowsSpecificError()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "seller@uv.mx", "Seller#1234");

        var overflowResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Products/Create",
            "/Products/Create",
            [
                new("Title", "Producto Prueba"),
                new("Description", "Descripción válida"),
                new("Price", "100.00"),
                new("Stock", "999999999999")
            ]);
        Assert.Equal(HttpStatusCode.OK, overflowResp.StatusCode);
        var overflowHtml = FrontendWebApplicationFactory.DecodeHtml(await overflowResp.Content.ReadAsStringAsync());
        Assert.Contains(">El número de existencias es demasiado grande (máximo 2,147,483,647).</span>", overflowHtml);
        Assert.DoesNotContain(">Las existencias deben ser mayores a 0", overflowHtml);

        var zeroResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Products/Create",
            "/Products/Create",
            [
                new("Title", "Producto Cero"),
                new("Description", "Descripción válida"),
                new("Price", "0"),
                new("Stock", "0")
            ]);
        Assert.Equal(HttpStatusCode.OK, zeroResp.StatusCode);
        var zeroHtml = FrontendWebApplicationFactory.DecodeHtml(await zeroResp.Content.ReadAsStringAsync());
        Assert.Contains("mayor a 0", zeroHtml);
        Assert.Contains("mayores a 0", zeroHtml);
        Assert.DoesNotContain(factory.Backend.Products.Values, p => p.Title == "Producto Cero");
    }

    [Fact]
    public async Task Seller_OrderCancelReason_RejectsOver255Chars()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "seller@uv.mx", "Seller#1234");

        factory.Backend.Orders.Add(new Order
        {
            Id = "901",
            BuyerId = factory.Backend.UsersByEmail["buyer@uv.mx"].Id,
            SellerId = factory.Backend.UsersByEmail["seller@uv.mx"].Id,
            Status = OrderStatus.Pending,
            Total = "1250.00",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = [new OrderItem { Id = "1", OrderId = "901", ProductId = 1, ProductTitle = "Teclado Mecánico", Quantity = 1, UnitPrice = "1250.00" }]
        });

        var overReasonResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Orders/Details/901",
            "/Orders/UpdateStatus/901",
            [
                new("status", OrderStatus.Canceled),
                new("reason", new string('a', 256))
            ]);
        Assert.Equal(HttpStatusCode.Redirect, overReasonResp.StatusCode);
        var detailsHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Orders/Details/901")).Content.ReadAsStringAsync());
        Assert.Contains("entre 1 y 255 caracteres", detailsHtml);
        Assert.NotEqual(OrderStatus.Canceled, factory.Backend.Orders.First(o => o.Id == "901").Status);
    }
}
