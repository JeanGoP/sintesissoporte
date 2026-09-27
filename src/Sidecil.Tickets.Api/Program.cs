using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Api;
using Sidecil.Tickets.Infrastructure.Mail;

var builder = WebApplication.CreateBuilder(args);
var frontendOrigin = FrontendHosting.Origin(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => {
    if (frontendOrigin.Length > 0) policy.WithOrigins(frontendOrigin)
        .AllowCredentials().WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
        .WithHeaders("Content-Type", "X-CSRF-TOKEN", "If-Match", "Authorization", "X-Sidecil-Widget")
        .WithExposedHeaders("ETag", "Location");
}));
var connection = builder.Configuration.GetConnectionString("Tickets")
    ?? throw new InvalidOperationException("Configura ConnectionStrings__Tickets.");
builder.Services.AddDbContext<TicketsDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o => {
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 12;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<TicketsDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o => {
    o.Cookie.Name = "Sidecil.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAntiforgery(o => {
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
var protection = builder.Services.AddDataProtection().SetApplicationName("Sidecil.Tickets");
if (builder.Configuration["DataProtection:Path"] is { Length: > 0 } keysPath) {
    protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
} else if (!builder.Environment.IsDevelopment()) {
    throw new InvalidOperationException("Configura DataProtection__Path fuera del directorio de despliegue.");
}
PortalIdentity.Configure(builder);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<UserInvitations>();
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(24));
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<EmailComposer>();
var mailConfiguration = builder.Configuration.GetSection("Mail").Get<MailOptions>() ?? new();
if (mailConfiguration.Mode is not ("Disabled" or "Pickup" or "Smtp"))
    throw new InvalidOperationException("Modo de correo inválido.");
if (!builder.Environment.IsDevelopment() && mailConfiguration.Mode == "Pickup")
    throw new InvalidOperationException("Pickup solo se permite en Development.");
if (mailConfiguration.Mode == "Smtp" && (!Uri.TryCreate(mailConfiguration.PublicBaseUrl, UriKind.Absolute, out var mailPublicUrl) || mailPublicUrl.Scheme != "https"))
    throw new InvalidOperationException("Configura Mail__PublicBaseUrl con la URL pública HTTPS del portal.");
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("chat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("guest", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (builder.Configuration["Hosting:PathBase"] is { Length: > 0 } apiPath) app.UsePathBase(apiPath);
if (args.Contains("--seed-demo") || args.Contains("--bootstrap")) {
    await Bootstrap.RunAsync(app.Services, app.Environment.IsDevelopment(), args.Contains("--seed-demo"));
    return;
}
if (args.Contains("--update-database")) {
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Database.MigrateAsync();
    Console.WriteLine("Base de datos actualizada correctamente."); return;
}
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) {
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) => {
    var ancestors = "'none'";
    if (context.Request.Path == "/chat-widget") {
        context.Response.Headers.CacheControl = "no-store";
        if (Guid.TryParse(context.Request.Query["site"], out var siteId)) {
            var db = context.RequestServices.GetRequiredService<TicketsDbContext>();
            var origin = await db.ChatSites.Where(x => x.Id == siteId && x.Enabled).Select(x => x.Origin).FirstOrDefaultAsync();
            if (origin is not null) ancestors = "'self' " + origin;
        }
    }
    if (context.Request.Path.StartsWithSegments("/auth/external") || context.Request.Path.Value?.StartsWith("/signin-") == true) {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    context.Response.Headers.XContentTypeOptions = "nosniff";
    if (!context.Response.Headers.ContainsKey("Referrer-Policy")) context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors {ancestors}; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseRouting();
app.UseCors("Frontend");
if (frontendOrigin.Length == 0) app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/api/v1/chat") && !HttpMethods.IsGet(context.Request.Method) &&
        !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method)) {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) {
            await Results.Problem("La sesión del formulario venció. Recarga e intenta nuevamente.", statusCode: 400).ExecuteAsync(context);
            return;
        }
    }
    await next();
});
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (TicketsDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapGet("/api/v1/auth/csrf", (HttpContext c, IAntiforgery antiforgery) =>
    Results.Ok(new { token = antiforgery.GetAndStoreTokens(c).RequestToken }));
app.MapPost("/api/v1/auth/login", async (LoginRequest request, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn) => {
    var user = await users.FindByEmailAsync(request.Email.Trim());
    if (user is null) return Results.Problem("Correo o contraseña incorrectos.", statusCode: 401);
    var result = await signIn.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
    return result.Succeeded ? Results.NoContent() : Results.Problem("Correo o contraseña incorrectos, o acceso temporalmente bloqueado.", statusCode: 401);
}).AddEndpointFilter<ValidationFilter>().RequireRateLimiting("login");
var api = app.MapGroup("/api/v1").RequireAuthorization().AddEndpointFilter<ValidationFilter>();
api.MapGet("/auth/me", async (HttpContext c, UserManager<ApplicationUser> users, TicketsDbContext db) => {
    var user = await users.GetUserAsync(c.User);
    if (user is null) return Results.Unauthorized();
    var organization = await db.Organizations.Where(x => x.Id == user.OrganizationId).Select(x => x.Name).FirstAsync();
    return Results.Ok(new { user.Id, user.DisplayName, user.Email, user.Role, user.OrganizationId, organization, user.TeamId, hasPassword = await users.HasPasswordAsync(user) });
});
api.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); });
api.MapPost("/auth/password", async (ChangePasswordRequest request, HttpContext c, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) => {
    var user = (await users.GetUserAsync(c.User))!;
    var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded) return Results.Problem("No fue posible cambiar la contraseña. Verifica la actual y utiliza 12 caracteres, mayúsculas, minúsculas, números y símbolos.", statusCode: 400);
    await signIn.RefreshSignInAsync(user);
    return Results.NoContent();
});
api.MapGet("/tickets", (HttpContext c, TicketService s, string? search, string? status, string? priority, string? view, int? page) =>
    s.ListAsync(c, search, status, priority, view, page ?? 1));
api.MapGet("/tickets/{id:guid}", (Guid id, HttpContext c, TicketService s) => s.DetailAsync(c, id));
api.MapPost("/tickets", (CreateTicketRequest r, HttpContext c, TicketService s) => s.CreateAsync(c, r));
api.MapPost("/tickets/{id:guid}/messages", (Guid id, AddMessageRequest r, HttpContext c, TicketService s) => s.MessageAsync(c, id, r));
api.MapPost("/tickets/{id:guid}/transitions", (Guid id, TransitionRequest r, HttpContext c, TicketService s) => s.TransitionAsync(c, id, r));
api.MapPut("/tickets/{id:guid}/assignment", (Guid id, AssignmentRequest r, HttpContext c, TicketService s) => s.AssignAsync(c, id, r));
api.MapGet("/directory", (HttpContext c, TicketService s) => s.DirectoryAsync(c));
api.MapPost("/admin/users", (CreateUserRequest r, HttpContext c, TicketService s) => s.CreateUserAsync(c, r));
api.MapPost("/admin/organizations", (CreateOrganizationRequest r, HttpContext c, TicketService s) => s.CreateOrganizationAsync(c, r));
api.MapPost("/admin/users/{id}/invitation", (string id, HttpContext c, UserInvitations invitations) => invitations.ResendAsync(id, c)).RequireRateLimiting("login");
app.MapPost("/api/v1/auth/invitation", (AcceptInvitationRequest r, UserInvitations invitations) => invitations.AcceptAsync(r)).AddEndpointFilter<ValidationFilter>().RequireRateLimiting("login");
api.MapPost("/auth/chat-session", ChatIdentity.PortalAsync).RequireRateLimiting("login");
PortalIdentity.Map(app);
ChatEndpoints.Map(app);
api.MapGet("/tickets/{id:guid}/attachments/{fileId:guid}", (Guid id, Guid fileId, HttpContext c, TicketService s) => s.AttachmentAsync(c, id, fileId));
EmailEndpoints.Map(app);
app.Map("/api/{**path}", () => Results.NotFound());
if (frontendOrigin.Length == 0) app.MapFallbackToFile("index.html");
else {
    // The embedded widget retains its per-integration framing policy on the API host.
    app.MapGet("/chat-widget", async (HttpContext context, IWebHostEnvironment env) => {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(env.WebRootPath, "index.html"));
    });
    app.MapGet("/", () => Results.Redirect(frontendOrigin + "/"));
}
await app.RunAsync();

public partial class Program { }
