namespace BarbershopCrm.Domain.Entities;

public class Booking
{
    public int BookingId { get; set; }
    public int ClientId { get; set; }
    public int MasterId { get; set; }
    public int ServiceId { get; set; }
    public int BranchId { get; set; }

    public DateTime StartDateTime { get; set; }
    public int DurationMinutes { get; set; }
    public decimal PriceSnapshot { get; set; }
    public decimal LoyaltyDiscountPercent { get; set; }
    public string LoyaltyDiscountReason { get; set; } = "None";

    public string Status { get; set; } = Domain.Enums.BookingStatus.Created;
    public string Source { get; set; } = Domain.Enums.BookingSource.Online;
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Client Client { get; set; } = null!;
    public Master Master { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Visit? Visit { get; set; }

    public DateTime EndDateTime => StartDateTime.AddMinutes(DurationMinutes);

    public decimal FinalPrice => PriceSnapshot * (1 - LoyaltyDiscountPercent / 100m);
}
