using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed partial class PortalOidcTests
{
    [Fact]
    public async Task VerifiedGuestChoosesOwnTicketOrCreatesNewWithoutSecondConfirmation()
    {
        using var factory = new PortalOidcFactory();
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Mail:Mode","Pickup"));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions {BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        var email = Guid.NewGuid()+"@test.invalid";
        var otherEmail = Guid.NewGuid()+"@test.invalid";
        long firstId=0, otherId=0;
        using (var scope=app.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var team=await db.Teams.Select(x=>x.Id).FirstAsync();
            var first=new Ticket {Subject="Problema propio",Category="General",ModuleId=Guid.Parse("d1000000-0000-0000-0000-000000000001"),CompanyName="Empresa de prueba",GuestName="Cliente",GuestEmail=email,OrganizationId=TicketsDbContext.GuestOrganizationId,TeamId=team,DueAt=DateTime.UtcNow.AddHours(24)};
            var other=new Ticket {Subject="Problema ajeno",GuestName="Otro",GuestEmail=otherEmail,OrganizationId=TicketsDbContext.GuestOrganizationId,TeamId=team,DueAt=DateTime.UtcNow.AddHours(24)};
            db.Tickets.AddRange(first,other); await db.SaveChangesAsync(); firstId=first.Id;otherId=other.Id;
        }
        string AccessToken(string body) => body.Split("#access=")[1].Split((char)10)[0].Trim();
        async Task<string> Start() {
            var response=await client.PostAsJsonAsync("/api/v1/public/access/start",new{name="Cliente",email});
            Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);
            using var scope=app.Services.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var mail=await db.OutboundEmails.Where(x=>x.Recipient==email&&x.Kind=="PublicAccess").OrderByDescending(x=>x.CreatedAt).FirstAsync();
            return AccessToken(mail.Body);
        }
        MultipartFormDataContent Form(string token,string ticketId,string subject="") {
            var form=new MultipartFormDataContent();
            form.Add(new StringContent(token),"token");form.Add(new StringContent(ticketId),"ticketId");
            form.Add(new StringContent(subject),"subject");form.Add(new StringContent("Nueva evidencia del problema"),"body");
            form.Add(new StringContent("General"),"category");form.Add(new StringContent("d1000000-0000-0000-0000-000000000001"),"moduleId");
            form.Add(new StringContent("Empresa de prueba"),"companyName");
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Detalle adicional")),"files","detalle.txt");
            return form;
        }
        try {
            await Csrf(client);
            var token=await Start();
            var lookup=await client.PostAsJsonAsync("/api/v1/public/access/tickets",new{token});
            lookup.EnsureSuccessStatusCode();
            using(var json=JsonDocument.Parse(await lookup.Content.ReadAsStringAsync())) {
                var tickets=json.RootElement.GetProperty("tickets").EnumerateArray().ToList();
                Assert.Single(tickets);
                Assert.Equal("Problema propio",tickets[0].GetProperty("subject").GetString());
            }
            using(var wrong=Form(token,(await Id(otherId)))) Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync("/api/v1/public/access/send",wrong)).StatusCode);
            using(var reply=Form(token,await Id(firstId))) (await client.PostAsync("/api/v1/public/access/send",reply)).EnsureSuccessStatusCode();
            using(var scope=app.Services.CreateScope()) {
                var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.Equal(1,await db.TicketAttachments.CountAsync(x=>x.TicketId==firstId));
                Assert.True((await db.Tickets.FindAsync(firstId))!.HasCustomerReply);
            }
            using(var repeat=Form(token,await Id(firstId))) Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync("/api/v1/public/access/send",repeat)).StatusCode);
            var second=await Start();
            using(var create=Form(second,"","Nuevo ticket de prueba")) (await client.PostAsync("/api/v1/public/access/send",create)).EnsureSuccessStatusCode();
            using(var scope=app.Services.CreateScope()) {
                var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.Equal(2,await db.Tickets.CountAsync(x=>x.GuestEmail==email));
                Assert.Equal(0,await db.GuestSubmissions.CountAsync(x=>x.Email==email));
            }
        } finally {
            using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.PublicGuestAccesses.Where(x=>x.Email==email).ExecuteDeleteAsync();
            await db.OutboundEmails.Where(x=>x.Recipient==email).ExecuteDeleteAsync();
            await db.Tickets.Where(x=>x.GuestEmail==email||x.GuestEmail==otherEmail).ExecuteDeleteAsync();
        }
        async Task<string> Id(long id) { using var scope=app.Services.CreateScope(); return (await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Tickets.FindAsync(id))!.PublicId.ToString(); }
    }
}
