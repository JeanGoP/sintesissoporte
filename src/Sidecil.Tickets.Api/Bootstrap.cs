using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;

namespace Sidecil.Tickets.Api;
public static class Bootstrap
{
    public static async Task RunAsync(IServiceProvider services, bool development, bool demo)
    {
        if (demo && !development) throw new InvalidOperationException("Los datos demo solo se permiten en Development.");
        var email = Environment.GetEnvironmentVariable("SIDECIL_ADMIN_EMAIL") ?? "admin@sidecil.local";
        var password = Environment.GetEnvironmentVariable("SIDECIL_BOOTSTRAP_PASSWORD")
            ?? throw new InvalidOperationException("Define SIDECIL_BOOTSTRAP_PASSWORD (mínimo 12 caracteres).");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // Migraciones únicamente en esta operación explícita, nunca durante el arranque normal.
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync()) {
            Console.WriteLine("La base ya tiene usuarios; no se alteraron cuentas ni contraseñas.");
            return;
        }
        await using var transaction = await db.Database.BeginTransactionAsync();
        var sidecil = new Organization { Name = "Sidecil" };
        var team = new Team { Name = "Atención Sidecil" };
        db.Organizations.Add(sidecil);
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        async Task<ApplicationUser> Create(string name, string address, string role, Guid organization, Guid? teamId = null) {
            var user = new ApplicationUser { DisplayName = name, UserName = address, Email = address, Role = role, OrganizationId = organization, TeamId = teamId };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            return user;
        }
        var admin = await Create("Administrador Sidecil", email, "Admin", sidecil.Id);
        if (demo) {
            var client = new Organization { Name = "Grupo Horizonte · Demo" };
            db.Organizations.Add(client);
            await db.SaveChangesAsync();
            var agent = await Create("Laura Gómez", "agente@sidecil.local", "Agent", sidecil.Id, team.Id);
            var employee = await Create("Daniel Torres", "empleado@sidecil.local", "Requester", sidecil.Id);
            var external = await Create("Mariana López", "cliente@sidecil.local", "Requester", client.Id);
            var examples = new[] {
                ("No puedo acceder al portal de facturación", "Al iniciar sesión aparece un mensaje de acceso denegado. Necesito consultar las facturas del mes.", "Accesos", TicketPriority.High, external, TicketStatus.InProgress),
                ("Configurar el correo en mi nuevo equipo", "Recibí un equipo nuevo y necesito configurar mi correo corporativo para continuar trabajando.", "Soporte técnico", TicketPriority.Normal, employee, TicketStatus.New),
                ("Confirmación del servicio programado", "Por favor confirmar la fecha y el horario de la visita técnica que solicitamos para nuestra sede.", "Servicios", TicketPriority.Normal, external, TicketStatus.WaitingRequester),
                ("Error al generar el informe mensual", "La exportación del informe se queda cargando y no permite descargar el archivo para el cierre.", "Soporte técnico", TicketPriority.Urgent, employee, TicketStatus.InProgress),
                ("Actualización de datos de facturación", "Necesitamos actualizar la dirección de facturación de nuestra organización antes del próximo corte.", "Facturación", TicketPriority.Low, external, TicketStatus.Resolved),
                ("Solicitud de acceso a carpeta compartida", "Solicito acceso de lectura a la carpeta del equipo comercial para revisar los documentos del proyecto.", "Accesos", TicketPriority.Normal, employee, TicketStatus.New)
            };
            var index = 0;
            foreach (var (subject, body, category, priority, requester, status) in examples) {
                var created = DateTime.UtcNow.AddHours(-(++index * 3));
                var ticket = new Ticket {
                    Subject = subject, Category = category, Priority = priority, RequesterId = requester.Id,
                    OrganizationId = requester.OrganizationId, TeamId = team.Id, Status = status,
                    AssigneeId = status == TicketStatus.New ? null : agent.Id,
                    CreatedAt = created, UpdatedAt = created.AddMinutes(20),
                    DueAt = created.AddHours(Ticket.TargetHours(priority)),
                    ResolvedAt = status == TicketStatus.Resolved ? created.AddHours(1) : null
                };
                ticket.Messages.Add(new TicketMessage { AuthorId = requester.Id, Body = body, CreatedAt = created });
                ticket.Events.Add(new TicketEvent { ActorId = requester.Id, Kind = "Created", Detail = "Solicitud de demostración", CreatedAt = created });
                if (status != TicketStatus.New) {
                    ticket.Messages.Add(new TicketMessage { AuthorId = agent.Id, Body = "Hola, ya recibimos tu solicitud. Estamos revisando el caso y te compartiremos los avances por este medio.", CreatedAt = created.AddMinutes(15) });
                    ticket.Messages.Add(new TicketMessage { AuthorId = agent.Id, Body = "Nota privada de demostración. Validar antecedentes antes de responder al solicitante.", Visibility = MessageVisibility.Internal, CreatedAt = created.AddMinutes(20) });
                }
                db.Tickets.Add(ticket);
            }
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        Console.WriteLine(demo ? "Base de demostración preparada." : "Administrador inicial creado.");
    }
}
