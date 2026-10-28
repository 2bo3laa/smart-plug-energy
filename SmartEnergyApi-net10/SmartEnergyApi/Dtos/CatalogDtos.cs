namespace SmartEnergyApi.Dtos;

public record CatalogTypeDto(string Key, string Name, double DefaultWatts, int Count);
public record CatalogBrandDto(string Name, int Count);
public record CatalogModelDto(int Id, string Model, double RatedWatts, string Source);
