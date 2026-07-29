using EtsyBacklogListingGenerator.Inventory;

namespace EtsyBacklogListingGenerator.Drafts;

public sealed class DraftListingRequest
{
    public required string Title { get; init; }

    // Etsy uses the listing title as the shopping-cart summary; it has no separate API field.
    public string CartSummary => Title;

    public required string Description { get; init; }

    public required long TaxonomyId { get; init; }

    public required string WhoMade { get; init; }

    public required string WhenMade { get; init; }

    public bool IsSupply { get; init; }

    public string ListingType { get; init; } = "physical";

    public long? ShippingProfileId { get; init; }

    public long? ReturnPolicyId { get; init; }

    public long? ShopSectionId { get; init; }

    public List<string> Tags { get; init; } = [];

    // Optional fallback for listings without per-finish ProcessingTime values.
    public long? ReadinessStateId { get; init; }

    public required FinishScalePriceStructure PriceStructure { get; init; }
}

public sealed class CreatedDraftListing
{
    public required long ListingId { get; init; }

    public required string State { get; init; }

    public string Url { get; init; } = string.Empty;
}

public sealed class DraftListingTemplate
{
    public required long TaxonomyId { get; init; }

    public required string WhoMade { get; init; }

    public required string WhenMade { get; init; }

    public bool IsSupply { get; init; }

    public string ListingType { get; init; } = "physical";

    public long? ShippingProfileId { get; init; }

    public long? ReturnPolicyId { get; init; }

    public long? ShopSectionId { get; init; }
}
