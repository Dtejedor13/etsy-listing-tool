namespace EtsyBacklogListingGenerator.Inventory;

/// <summary>
/// A finish-first pricing structure for Etsy's two custom listing variations.
/// </summary>
public sealed class FinishScalePriceStructure
{
    public const long FinishPropertyId = 513;
    public const long ScalePropertyId = 514;

    public List<FinishPricing> Finishes { get; init; } = [];

    public List<long> PriceOnProperty { get; init; } = [FinishPropertyId, ScalePropertyId];

    public List<long> QuantityOnProperty { get; init; } = [];

    public List<long> SkuOnProperty { get; init; } = [];

    public List<long> ReadinessStateOnProperty { get; init; } = [FinishPropertyId, ScalePropertyId];

    internal EtsyInventoryUpdateRequest ToEtsyRequest()
    {
        if (Finishes.Count == 0)
            throw new InvalidOperationException("At least one finish is required.");

        var products = new List<EtsyInventoryProduct>();
        foreach (var finish in Finishes)
        {
            if (string.IsNullOrWhiteSpace(finish.Name))
                throw new InvalidOperationException("Each finish must have a name.");
            if (finish.ReadinessStateId is not > 0)
                throw new InvalidOperationException($"Finish '{finish.Name}' must have a readiness state ID.");
            if (finish.Scales.Count == 0)
                throw new InvalidOperationException($"Finish '{finish.Name}' must have at least one scale.");

            foreach (var scale in finish.Scales)
            {
                if (string.IsNullOrWhiteSpace(scale.Name))
                    throw new InvalidOperationException($"A scale for finish '{finish.Name}' has no name.");
                if (scale.Price <= 0)
                    throw new InvalidOperationException($"The price for '{finish.Name}' / '{scale.Name}' must be greater than zero.");
                if (scale.Quantity < 0)
                    throw new InvalidOperationException($"The quantity for '{finish.Name}' / '{scale.Name}' cannot be negative.");

                products.Add(new EtsyInventoryProduct
                {
                    Sku = scale.Sku,
                    PropertyValues =
                    [
                        new EtsyPropertyValue
                        {
                            PropertyId = FinishPropertyId,
                            PropertyName = "Finish",
                            ValueIds = ToValueIds(finish.ValueId),
                            Values = [finish.Name]
                        },
                        new EtsyPropertyValue
                        {
                            PropertyId = ScalePropertyId,
                            PropertyName = "Scale",
                            ValueIds = ToValueIds(scale.ValueId),
                            Values = [scale.Name]
                        }
                    ],
                    Offerings =
                    [
                        new EtsyOffering
                        {
                            Price = scale.Price,
                            Quantity = scale.Quantity,
                            IsEnabled = scale.IsEnabled,
                            ReadinessStateId = finish.ReadinessStateId.Value
                        }
                    ]
                });
            }
        }

        return new EtsyInventoryUpdateRequest
        {
            Products = products,
            PriceOnProperty = PriceOnProperty,
            QuantityOnProperty = QuantityOnProperty,
            SkuOnProperty = SkuOnProperty,
            ReadinessStateOnProperty = ReadinessStateOnProperty
        };
    }

    internal FinishScalePriceStructure ForNewListing() => new()
    {
        PriceOnProperty = [.. PriceOnProperty],
        QuantityOnProperty = [.. QuantityOnProperty],
        SkuOnProperty = [.. SkuOnProperty],
        ReadinessStateOnProperty = [.. ReadinessStateOnProperty],
        Finishes = Finishes.Select(finish => new FinishPricing
        {
            Name = finish.Name,
            ReadinessStateId = finish.ReadinessStateId,
            ProcessingTime = finish.ProcessingTime,
            Scales = finish.Scales.Select(scale => new ScalePricing
            {
                Name = scale.Name,
                Price = scale.Price,
                Quantity = scale.Quantity,
                IsEnabled = scale.IsEnabled,
                Sku = scale.Sku
            }).ToList()
        }).ToList()
    };

    private static List<long> ToValueIds(long? valueId) => valueId is null ? [] : [valueId.Value];
}

public sealed class FinishPricing
{
    public required string Name { get; init; }

    // Keep this when editing an existing listing; leave null for a new custom variation value.
    public long? ValueId { get; init; }

    // Use an existing Etsy profile when editing a listing. For generated listings,
    // ProcessingTime lets EtsyClient create or reuse the appropriate profile.
    public long? ReadinessStateId { get; init; }

    public ProcessingTime? ProcessingTime { get; init; }

    public List<ScalePricing> Scales { get; init; } = [];
}

/// <summary>
/// Etsy's processing-time window for made-to-order offerings.
/// </summary>
public sealed class ProcessingTime
{
    public required int Minimum { get; init; }

    public required int Maximum { get; init; }

    public ProcessingTimeUnit Unit { get; init; } = ProcessingTimeUnit.Days;
}

public enum ProcessingTimeUnit
{
    Days,
    Weeks
}

public sealed class ScalePricing
{
    public required string Name { get; init; }

    // Keep this when editing an existing listing; leave null for a new custom variation value.
    public long? ValueId { get; init; }

    public required decimal Price { get; init; }

    public int Quantity { get; init; } = 10;

    public bool IsEnabled { get; init; } = true;

    public string Sku { get; init; } = string.Empty;
}

internal sealed class EtsyInventoryUpdateRequest
{
    public required List<EtsyInventoryProduct> Products { get; init; }

    public required List<long> PriceOnProperty { get; init; }

    public required List<long> QuantityOnProperty { get; init; }

    public required List<long> SkuOnProperty { get; init; }

    public required List<long> ReadinessStateOnProperty { get; init; }
}

internal sealed class EtsyInventoryProduct
{
    public string Sku { get; init; } = string.Empty;

    public required List<EtsyPropertyValue> PropertyValues { get; init; }

    public required List<EtsyOffering> Offerings { get; init; }
}

internal sealed class EtsyPropertyValue
{
    public required long PropertyId { get; init; }

    public required string PropertyName { get; init; }

    public long? ScaleId { get; init; }

    public required List<long> ValueIds { get; init; }

    public required List<string> Values { get; init; }
}

internal sealed class EtsyOffering
{
    public required decimal Price { get; init; }

    public required int Quantity { get; init; }

    public required bool IsEnabled { get; init; }

    public required long ReadinessStateId { get; init; }
}
