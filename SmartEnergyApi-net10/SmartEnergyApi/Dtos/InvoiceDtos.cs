namespace SmartEnergyApi.Dtos;

public record InvoiceLineDto(int DeviceId, string Name, string Room, double Hours, double PeakWatts, double Kwh, double CostEgp, double SharePercent);

/// <summary>One band of the stepped tariff (monthly invoices only).</summary>
public record InvoiceBandDto(int Tier, double FromKwh, double? ToKwh, double Price, double Kwh, double CostEgp);

public record InvoiceDto(
    string Number,
    string Period,
    DateTime From,
    DateTime To,
    bool SingleDevice,
    double TotalKwh,
    double TotalCostEgp,
    double HomeKwh,
    double HomeCostEgp,
    double PricePerKwh,
    int TierNumber,
    List<InvoiceLineDto> Lines,
    List<InvoiceBandDto> Bands
);

public record EmailInvoiceRequest(string? Period, int? DeviceId, string? Lang);
