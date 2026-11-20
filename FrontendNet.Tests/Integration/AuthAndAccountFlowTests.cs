using System.Net;
using FrontendNet.Models;
using FrontendNet.Tests.Support;

namespace FrontendNet.Tests.Integration;

public class AuthAndAccountFlowTests
{
    [Theory]
    [InlineData("buyer@uv.mx", "Buyer#1234", "/Shopping")]
    [InlineData("seller@uv.mx", "Seller#1234", "/Products")]
    [InlineData("pending-seller@uv.mx", "Seller#1234", "/Profile")]
    [InlineData("admin@uv.mx", "Admin#1234", "/Orders")]
    public async Task Login_RedirectsAccordingToRoleAndSellerApproval(
        string email,
        string password,
        string expectedLocation)
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        var resp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth",
            "/Auth",
            [
                new("Email", email),
                new("Password", password)
            ]);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal(expectedLocation, resp.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_InvalidCredentials_AndRateLimit_ShowSpanishMessages()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        var badResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth",
            "/Auth",
            [
                new("Email", "buyer@uv.mx"),
                new("Password", "Wrong#999")
            ]);
        Assert.Equal(HttpStatusCode.OK, badResp.StatusCode);
        var badHtml = FrontendWebApplicationFactory.DecodeHtml(await badResp.Content.ReadAsStringAsync());
        Assert.Contains("Credenciales inválidas", badHtml);

        factory.Backend.SimulateRateLimitOnAuthSeconds = 120;
        var limitResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth",
            "/Auth",
            [
                new("Email", "buyer@uv.mx"),
                new("Password", "Buyer#1234")
            ]);
        Assert.Equal(HttpStatusCode.OK, limitResp.StatusCode);
        var limitHtml = FrontendWebApplicationFactory.DecodeHtml(await limitResp.Content.ReadAsStringAsync());
        Assert.Contains("Demasiados intentos", limitHtml);
        Assert.Contains("2 minuto(s)", limitHtml);
    }

    [Fact]
    public async Task ExpiredRevokedSession_SignsUserOutCleanlyWithoutZombieCookie()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#1234");
        factory.Backend.RevokeAllUserSessions(factory.Backend.UsersByEmail["buyer@uv.mx"].Id);
        factory.Backend.ForceNextProtectedCallUnauthorized = true;

        var shoppingResp = await client.GetAsync("/Shopping");
        Assert.Equal(HttpStatusCode.Redirect, shoppingResp.StatusCode);
        Assert.Contains("/Auth", shoppingResp.Headers.Location?.OriginalString);

        // Ensure cookie was actually cleared so the next request is also unauthenticated.
        var followUp = await client.GetAsync("/Profile");
        Assert.Equal(HttpStatusCode.Redirect, followUp.StatusCode);
        Assert.Contains("/Auth", followUp.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task RegisterAndVerifyOtp_WorksForBuyerAndSeller()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient(allowAutoRedirect: true);

        // Register Buyer (should omit storeName)
        var regBuyer = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth/Register",
            "/Auth/Register",
            [
                new("Profile", Roles.Buyer),
                new("Name", "Elena Compradora"),
                new("Email", "elena@uv.mx"),
                new("Password", "Elena#1234"),
                new("ConfirmPassword", "Elena#1234"),
                new("PhoneNumber", "2281234567")
            ]);
        Assert.Equal(HttpStatusCode.OK, regBuyer.StatusCode);
        Assert.True(factory.Backend.RegistrationOtps.ContainsKey("elena@uv.mx"));

        var verifyBuyer = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth/VerifyRegistration?email=elena%40uv.mx",
            "/Auth/VerifyRegistration",
            [
                new("Email", "elena@uv.mx"),
                new("Code", "123456")
            ]);
        var verifyBuyerHtml = FrontendWebApplicationFactory.DecodeHtml(await verifyBuyer.Content.ReadAsStringAsync());
        Assert.Contains("Cuenta verificada correctamente", verifyBuyerHtml);

        using var buyerLoginClient = factory.CreateBrowserClient();
        await factory.LoginAsync(buyerLoginClient, "elena@uv.mx", "Elena#1234");

        // Register Seller (requires storeName)
        using var sellerClient = factory.CreateBrowserClient(allowAutoRedirect: true);
        var regSeller = await factory.PostFormWithAntiforgeryAsync(
            sellerClient,
            "/Auth/Register",
            "/Auth/Register",
            [
                new("Profile", Roles.Seller),
                new("Name", "Carlos Vendedor"),
                new("StoreName", "Tienda Carlos"),
                new("Email", "carlos@uv.mx"),
                new("Password", "Carlos#1234"),
                new("ConfirmPassword", "Carlos#1234")
            ]);
        Assert.Equal(HttpStatusCode.OK, regSeller.StatusCode);

        var verifySeller = await factory.PostFormWithAntiforgeryAsync(
            sellerClient,
            "/Auth/VerifyRegistration?email=carlos%40uv.mx",
            "/Auth/VerifyRegistration",
            [
                new("Email", "carlos@uv.mx"),
                new("Code", "123456")
            ]);
        var verifySellerHtml = FrontendWebApplicationFactory.DecodeHtml(await verifySeller.Content.ReadAsStringAsync());
        Assert.Contains("Un administrador debe aprobar tu tienda", verifySellerHtml);

        using var sellerLoginClient = factory.CreateBrowserClient();
        await factory.LoginAsync(sellerLoginClient, "carlos@uv.mx", "Carlos#1234");
    }

    [Fact]
    public async Task ForgotAndResetPassword_AllowsLoginWithNewPassword()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient(allowAutoRedirect: true);

        var forgotResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth/ForgotPassword",
            "/Auth/ForgotPassword",
            [new("Email", "buyer@uv.mx")]);
        Assert.Equal(HttpStatusCode.OK, forgotResp.StatusCode);
        Assert.True(factory.Backend.RecoveryOtps.ContainsKey("buyer@uv.mx"));

        var resetResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth/ResetPassword?email=buyer%40uv.mx",
            "/Auth/ResetPassword",
            [
                new("Email", "buyer@uv.mx"),
                new("Code", "654321"),
                new("NewPassword", "NewBuyer#99"),
                new("ConfirmPassword", "NewBuyer#99")
            ]);
        var resetHtml = FrontendWebApplicationFactory.DecodeHtml(await resetResp.Content.ReadAsStringAsync());
        Assert.Contains("Contraseña restablecida correctamente", resetHtml);

        using var freshClient = factory.CreateBrowserClient();
        await factory.LoginAsync(freshClient, "buyer@uv.mx", "NewBuyer#99");
    }

    [Fact]
    public async Task ProfileViewEditAndChangePassword_WorkWithoutExposingJwt()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#1234");

        var profileResp = await client.GetAsync("/Profile");
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);
        var profileHtml = FrontendWebApplicationFactory.DecodeHtml(await profileResp.Content.ReadAsStringAsync());
        Assert.Contains("Comprador Demo", profileHtml);
        Assert.Contains("00:04:59", profileHtml);
        Assert.Contains("Comprador", profileHtml);
        Assert.DoesNotContain("eyJ", profileHtml);

        var editResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Profile/Edit",
            "/Profile/Edit",
            [
                new("Name", "Comprador Actualizado"),
                new("PhoneNumber", "2289998877")
            ]);
        Assert.Equal(HttpStatusCode.Redirect, editResp.StatusCode);

        var updatedProfileHtml = FrontendWebApplicationFactory.DecodeHtml(await (await client.GetAsync("/Profile")).Content.ReadAsStringAsync());
        Assert.Contains("Comprador Actualizado", updatedProfileHtml);
        Assert.Contains("2289998877", updatedProfileHtml);

        var pwdResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Profile/ChangePassword",
            "/Profile/ChangePassword",
            [
                new("CurrentPassword", "Buyer#1234"),
                new("NewPassword", "Buyer#5678"),
                new("ConfirmPassword", "Buyer#5678")
            ]);
        Assert.Equal(HttpStatusCode.Redirect, pwdResp.StatusCode);
        Assert.Contains("/Auth", pwdResp.Headers.Location?.OriginalString);

        // Signed out locally after password change
        var afterPwd = await client.GetAsync("/Profile");
        Assert.Equal(HttpStatusCode.Redirect, afterPwd.StatusCode);

        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#5678");
    }

    [Fact]
    public async Task Seller_ProfileEdit_RejectsBlankStoreName_AndUpdatesStoreName()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "seller@uv.mx", "Seller#1234");

        var blankStoreResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Profile/Edit",
            "/Profile/Edit",
            [
                new("Name", "Vendedor Demo"),
                new("PhoneNumber", "2281112233"),
                new("StoreName", "   ")
            ]);
        Assert.Equal(HttpStatusCode.OK, blankStoreResp.StatusCode);
        var blankStoreHtml = FrontendWebApplicationFactory.DecodeHtml(await blankStoreResp.Content.ReadAsStringAsync());
        Assert.Contains("tienda", blankStoreHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            factory.Backend.Requests,
            r => r.Method == "PUT" && r.PathAndQuery == "/api/users/me");

        var validEditResp = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Profile/Edit",
            "/Profile/Edit",
            [
                new("Name", "Vendedor Actualizado"),
                new("PhoneNumber", "2281112233"),
                new("StoreName", "Tienda Demo Renovada")
            ]);
        Assert.Equal(HttpStatusCode.Redirect, validEditResp.StatusCode);
        Assert.Equal("/Profile", validEditResp.Headers.Location?.OriginalString);

        var putMe = Assert.Single(
            factory.Backend.Requests,
            r => r.Method == "PUT" && r.PathAndQuery == "/api/users/me");
        Assert.Contains("\"storeName\":\"Tienda Demo Renovada\"", putMe.Body);

        var updatedProfileHtml = FrontendWebApplicationFactory.DecodeHtml(
            await (await client.GetAsync("/Profile")).Content.ReadAsStringAsync());
        Assert.Contains("Vendedor Actualizado", updatedProfileHtml);
        Assert.Contains("Tienda Demo Renovada", updatedProfileHtml);
    }
}
