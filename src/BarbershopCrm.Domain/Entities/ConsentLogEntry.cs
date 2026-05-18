namespace BarbershopCrm.Domain.Entities;

public class ConsentLogEntry
{
    public int ConsentId { get; set; }
    public int PersonaId { get; set; }
    public string ConsentType { get; set; } = null!;
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    public Persona Persona { get; set; } = null!;
}
