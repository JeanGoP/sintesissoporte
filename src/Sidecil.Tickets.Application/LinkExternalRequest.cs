using System.ComponentModel.DataAnnotations;
namespace Sidecil.Tickets.Application;
public record LinkExternalRequest([property: Required, StringLength(256)] string Password);
