using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;

namespace MarwanQuTech.WebApi.IntegrationTests;

public class PublicApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_reports_the_database_as_reachable()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_slug_is_a_problem_details_404_with_a_code()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/api/projects/does-not-exist?lang=en");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await Code(response)).ShouldBe("Project.NotFound");
    }

    [Fact]
    public async Task Lists_are_paged_and_language_is_bound_case_insensitively()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/api/articles?lang=EN&page=1&pageSize=5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("pageSize").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task Invalid_query_returns_validation_problem()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/api/projects?pageSize=500");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("errors").TryGetProperty("PageSize", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApi_document_exposes_the_bearer_scheme()
    {
        var json = await factory.CreateBrowserClient().GetStringAsync("/openapi/v1.json");

        json.ShouldContain("\"Bearer\"");
        json.ShouldContain("/api/admin/projects");

        // Lock icon only on protected operations.
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");
        paths.GetProperty("/api/projects").GetProperty("get").TryGetProperty("security", out _).ShouldBeFalse();
        paths.GetProperty("/api/admin/projects").GetProperty("get").TryGetProperty("security", out _).ShouldBeTrue();
        paths.GetProperty("/api/admin/projects/{id}").GetProperty("put").TryGetProperty("security", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Acme_challenge_files_are_served_over_plain_http()
    {
        var contentRoot = factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().ContentRootPath;
        var folder = Path.Combine(contentRoot, ".well-known", "acme-challenge");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "test-token-123"), "test-token-123.key");

        try
        {
            var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://localhost"),
                AllowAutoRedirect = false
            });
            var response = await client.GetAsync("/.well-known/acme-challenge/test-token-123");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).ShouldBe("test-token-123.key");
        }
        finally
        {
            File.Delete(Path.Combine(folder, "test-token-123"));
        }
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/api/sitemap");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
    }

    internal static async Task<string?> Code(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}

public class ContactFormTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static object Inquiry(string details = "أحتاج موقعاً تعريفياً لشركتي الصغيرة") => new
    {
        clientName = "مروان",
        email = "client@example.com",
        phone = "٠٧٩١٢٣٤٥٦٧",
        serviceType = "WebDevelopment", // enums accept names (and numbers)
        details,
        language = "Ar",
        website = ""
    };

    [Fact]
    public async Task Contact_form_validates_accepts_and_is_rate_limited()
    {
        var client = factory.CreateBrowserClient();

        var invalid = await client.PostAsJsonAsync("/api/inquiries", Inquiry(details: "قصير"));
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        for (var i = 0; i < 4; i++)
            (await client.PostAsJsonAsync("/api/inquiries", Inquiry())).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // 6th request in the window (the invalid one counted too).
        var limited = await client.PostAsJsonAsync("/api/inquiries", Inquiry());
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await PublicApiTests.Code(limited)).ShouldBe("RateLimit.Exceeded");
        limited.Headers.Contains("Retry-After").ShouldBeTrue();
    }
}

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Admin_endpoints_require_a_token()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/api/admin/dashboard");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_password_is_401_with_a_code()
    {
        var response = await factory.CreateBrowserClient().PostAsJsonAsync(
            "/api/auth/login", new { email = ApiFactory.AdminEmail, password = "wrong-password-1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await PublicApiTests.Code(response)).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_sets_an_httponly_cookie_that_refreshes_and_logout_clears_it()
    {
        var client = factory.CreateBrowserClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("mqt_refresh="));
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        setCookie.ShouldContain("path=/api/auth", Case.Insensitive);

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("refreshToken", out _).ShouldBeFalse(); // never in the body

        // The cookie jar sends mqt_refresh automatically.
        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await refresh.Content.ReadFromJsonAsync<ApiFactory.LoginBody>())!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/admin/dashboard")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PostAsync("/api/auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterLogout = await client.PostAsync("/api/auth/refresh", null);
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

public class AdminApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static object Project(string titleEn, string? summaryEn = "Summary") => new
    {
        titleAr = "مشروع " + titleEn,
        titleEn,
        summaryAr = "ملخص",
        summaryEn,
        descriptionAr = "وصف",
        descriptionEn = "Description",
        isReal = true,
        displayOrder = 0
    };

    [Fact]
    public async Task Create_returns_201_with_location_and_duplicate_slug_is_409()
    {
        var admin = await factory.CreateAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/projects", Project("Portfolio"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location!.ToString().ShouldContain("/api/admin/projects/");

        var duplicate = await admin.PostAsJsonAsync("/api/admin/projects", Project("Portfolio"));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await PublicApiTests.Code(duplicate)).ShouldBe("Project.SlugAr.Taken");
    }

    [Fact]
    public async Task Publishing_an_incomplete_project_is_422_and_a_complete_one_becomes_public()
    {
        var admin = await factory.CreateAdminClientAsync();

        var draft = await (await admin.PostAsJsonAsync("/api/admin/projects", Project("Draft", summaryEn: null)))
            .Content.ReadFromJsonAsync<JsonElement>();
        var publishDraft = await admin.PostAsync($"/api/admin/projects/{draft.GetProperty("id").GetInt32()}/publish", null);
        publishDraft.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await PublicApiTests.Code(publishDraft)).ShouldBe("Project.NotFullyLocalized");

        var full = await (await admin.PostAsJsonAsync("/api/admin/projects", Project("Shop")))
            .Content.ReadFromJsonAsync<JsonElement>();
        (await admin.PostAsync($"/api/admin/projects/{full.GetProperty("id").GetInt32()}/publish", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var page = await factory.CreateBrowserClient().GetAsync("/api/projects/shop?lang=en");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cover_upload_is_converted_and_served_from_uploads()
    {
        var admin = await factory.CreateAdminClientAsync();
        var project = await (await admin.PostAsJsonAsync("/api/admin/projects", Project("Cover Test")))
            .Content.ReadFromJsonAsync<JsonElement>();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png(400, 300));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "cover.png");

        var upload = await admin.PutAsync($"/api/admin/projects/{project.GetProperty("id").GetInt32()}/cover", form);
        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        var url = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        url.ShouldEndWith(".webp");

        var served = await factory.CreateBrowserClient().GetAsync(url);
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        served.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(365));
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Orange);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
