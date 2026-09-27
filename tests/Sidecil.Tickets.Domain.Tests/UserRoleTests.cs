using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sidecil.Tickets.Infrastructure;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed partial class PortalOidcTests
{
    [Fact]
    public async Task RoleChangesRequireAdminAndRevokeExistingSession()
    {
        using var factory = new PortalOidcFactory();
        HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions {BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        using var admin = Client(); using var customer = Client();
        ApplicationUser owner, target;
        using (var scope = factory.Services.CreateScope()) {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            async Task<ApplicationUser> Create(string role) {
                var email = Guid.NewGuid()+"@test.invalid";
                var user = new ApplicationUser {UserName=email,Email=email,DisplayName="Cambio de rol",Role=role,OrganizationId=TicketsDbContext.GuestOrganizationId};
                Assert.True((await users.CreateAsync(user,Password)).Succeeded); factory.CreatedUsers.Add(user.Id); return user;
            }
            owner=await Create("Admin"); target=await Create("Requester");
        }
        async Task Login(HttpClient client, ApplicationUser user) {
            await Csrf(client); (await client.PostAsJsonAsync("/api/v1/auth/login",new {email=user.Email,password=Password})).EnsureSuccessStatusCode(); await Csrf(client);
        }
        await Login(admin,owner); await Login(customer,target);
        string Path(string id) => "/api/v1/admin/catalog/users/"+id+"/role";
        Assert.Equal(HttpStatusCode.Forbidden,(await customer.PutAsJsonAsync(Path(target.Id),new {role="Admin"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync(Path(owner.Id),new {role="Requester"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PutAsJsonAsync(Path(target.Id),new {role="Agent",moduleIds=Array.Empty<Guid>()})).StatusCode);
        (await admin.PutAsJsonAsync(Path(target.Id),new {role="Admin"})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await customer.GetAsync("/api/v1/auth/me")).StatusCode);
        using (var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.Equal("Admin",(await db.Users.FindAsync(target.Id))!.Role);
            Assert.True(await db.UserClaims.AnyAsync(c=>c.UserId==target.Id && c.ClaimType=="Sidecil.RoleChanged"));
        }
        await Login(customer,target);
        (await customer.GetAsync("/api/v1/admin/catalog")).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync(Path(target.Id),new {role="Requester"})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await customer.GetAsync("/api/v1/admin/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PutAsJsonAsync(Path(target.Id),new {role="SuperAdmin"})).StatusCode);
        var moduleId=Guid.Parse("d1000000-0000-0000-0000-000000000001");
        (await admin.PutAsJsonAsync(Path(target.Id),new {role="Agent",moduleIds=new[]{moduleId}})).EnsureSuccessStatusCode();
        using (var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.Equal("Agent",(await db.Users.FindAsync(target.Id))!.Role);
            Assert.True(await db.AgentModules.AnyAsync(m=>m.UserId==target.Id && m.ModuleId==moduleId));
        }
    }
}
