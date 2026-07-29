namespace EtsyBacklogListingGenerator.Drafts;

public sealed class UploadedListingImage
{
    public required long ListingId { get; init; }

    public required long ListingImageId { get; init; }

    public required int Rank { get; init; }

    public string FullImageUrl { get; init; } = string.Empty;
}
