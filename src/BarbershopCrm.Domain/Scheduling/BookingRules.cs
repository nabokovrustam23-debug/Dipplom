using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Domain.Scheduling;

public static class BookingRules
{
    private static readonly TimeSpan NoShowBuffer = TimeSpan.FromMinutes(15);

    public static bool CanConfirm(Booking booking) =>
        booking.Status == BookingStatus.Created
        && DateTime.Now < booking.StartDateTime;

    public static bool CanCancel(Booking booking, bool isAdmin) =>
        isAdmin
        && (booking.Status is BookingStatus.Created or BookingStatus.Confirmed)
        && DateTime.Now < booking.StartDateTime;

    public static bool CanComplete(Booking booking) =>
        (booking.Status is BookingStatus.Created or BookingStatus.Confirmed)
        && DateTime.Now >= booking.StartDateTime;

    public static bool CanNoShow(Booking booking) =>
        (booking.Status is BookingStatus.Created or BookingStatus.Confirmed)
        && DateTime.Now >= booking.StartDateTime.Add(NoShowBuffer);
}
