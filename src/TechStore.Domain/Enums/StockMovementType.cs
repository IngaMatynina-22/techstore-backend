namespace TechStore.Domain.Enums;

public enum StockMovementType
{
    Receipt = 0,
    Sale = 1,
    Adjustment = 2,
    Reservation = 3,
    ReleaseReservation = 4,
    TransferOut = 5,
    TransferIn = 6,
    WriteOff = 7,
    Return = 8
}
