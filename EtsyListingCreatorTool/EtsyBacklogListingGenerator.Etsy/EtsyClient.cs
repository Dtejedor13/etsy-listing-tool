using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EtsyBacklogListingGenerator.Auth;
using EtsyBacklogListingGenerator.Drafts;
using EtsyBacklogListingGenerator.Inventory;

namespace EtsyBacklogListingGenerator
{
    public static class JsonStorage
    {
        public static async Task SaveAsync(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await File.WriteAllTextAsync(path, json);
        }
    }

    public sealed class EtsyClient
    {
        private static readonly JsonSerializerOptions EtsyJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
        public static readonly string SHOPSECTIONS = "application/shops/{_shopId}/sections";
        public static readonly string SHOPINFO = "application/shops/{_shopId}";
        public static readonly string GETLISTINGS = "application/shops/{_shopId}/listings";
        public static readonly string PING = "application/openapi-ping";
        private readonly HttpClient _http;
        private string _shopId = string.Empty;
        private readonly OAuthService oAuthService;

        public EtsyClient()
        {
            _shopId = Environment.GetEnvironmentVariable("ETSY_SHOP_ID") ?? string.Empty;
            _http = new HttpClient();
            var clientId = Environment.GetEnvironmentVariable("ETSY_CLIENT_ID")
                ?? throw new InvalidOperationException("Set ETSY_CLIENT_ID before creating an Etsy client.");
            var clientSecret = Environment.GetEnvironmentVariable("ETSY_CLIENT_SECRET")
                ?? throw new InvalidOperationException("Set ETSY_CLIENT_SECRET before creating an Etsy client.");
            var apiKey = $"{clientId}:{clientSecret}";
            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            oAuthService = new OAuthService(_http);
        }

        public async Task AuthenticateAsync()
        {
            var token = await oAuthService.GetTokenAsync();

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.AccessToken);

            if (string.IsNullOrWhiteSpace(_shopId))
                _shopId = await ResolveShopIdAsync(token.AccessToken);
        }

        public async Task<T> GetAsync<T>(string endpoint)
        {
            
            var response = await _http.GetAsync(CreateAPIRoute(endpoint));

            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<T>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                }) ?? throw new InvalidOperationException("Etsy returned an empty response.");
        }

        public async Task<string> GetRawJsonAsync(string endpoint)
        {
            var fullRoute = CreateAPIRoute(endpoint);
            var response = await _http.GetAsync(fullRoute);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Replaces a listing's complete inventory with the supplied finish/scale combinations.
        /// Use this for an existing listing or immediately after creating a draft listing.
        /// </summary>
        public async Task<string> UpdateListingInventoryAsync(
            long listingId,
            FinishScalePriceStructure priceStructure,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
            ArgumentNullException.ThrowIfNull(priceStructure);

            var endpoint = CreateAPIRoute($"application/listings/{listingId}/inventory");
            var resolvedPriceStructure = await ResolveProcessingTimesAsync(priceStructure, cancellationToken);
            var payload = resolvedPriceStructure.ToEtsyRequest();
            using var response = await _http.PutAsJsonAsync(
                endpoint,
                payload,
                EtsyJsonOptions,
                cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Etsy inventory update failed ({(int)response.StatusCode}): {responseContent}");
            }

            return responseContent;
        }

        public async Task<FinishScalePriceStructure> GetPriceStructureFromListingAsync(
            long listingId,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);

            var endpoint = CreateAPIRoute($"application/listings/{listingId}/inventory");
            using var response = await _http.GetAsync(endpoint, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Etsy inventory request failed ({(int)response.StatusCode}): {responseContent}");
            }

            var inventory = JsonSerializer.Deserialize<EtsyInventoryResponse>(responseContent, EtsyJsonOptions)
                ?? throw new InvalidOperationException("Etsy returned an empty inventory response.");

            var finishes = new List<FinishPricing>();
            foreach (var product in inventory.Products.Where(product => !product.IsDeleted))
            {
                var finish = GetPropertyValue(product, FinishScalePriceStructure.FinishPropertyId, "Finish");
                var scale = GetPropertyValue(product, FinishScalePriceStructure.ScalePropertyId, "Scale");
                var offering = product.Offerings.SingleOrDefault(offering => !offering.IsDeleted)
                    ?? throw new InvalidOperationException(
                        $"Product {product.ProductId} does not have an active offering.");

                if (offering.Price.Divisor <= 0)
                    throw new InvalidOperationException($"Product {product.ProductId} has an invalid Etsy price divisor.");
                if (offering.ReadinessStateId <= 0)
                    throw new InvalidOperationException($"Product {product.ProductId} does not have a readiness state ID.");

                var finishValueId = finish.ValueIds.FirstOrDefault();
                var matchingFinish = finishes.SingleOrDefault(item =>
                    item.Name == finish.Values.FirstOrDefault() &&
                    item.ValueId == (finishValueId == 0 ? null : finishValueId) &&
                    item.ReadinessStateId == offering.ReadinessStateId);

                if (matchingFinish is null)
                {
                    matchingFinish = new FinishPricing
                    {
                        Name = GetSingleValue(finish, product.ProductId),
                        ValueId = finishValueId == 0 ? null : finishValueId,
                        ReadinessStateId = offering.ReadinessStateId,
                        Scales = []
                    };
                    finishes.Add(matchingFinish);
                }

                var scaleValueId = scale.ValueIds.FirstOrDefault();
                matchingFinish.Scales.Add(new ScalePricing
                {
                    Name = GetSingleValue(scale, product.ProductId),
                    ValueId = scaleValueId == 0 ? null : scaleValueId,
                    Price = (decimal)offering.Price.Amount / offering.Price.Divisor,
                    Quantity = offering.Quantity,
                    IsEnabled = offering.IsEnabled,
                    Sku = product.Sku
                });
            }

            if (finishes.Count == 0)
                throw new InvalidOperationException("The listing does not have an active Finish/Scale inventory structure.");

            return new FinishScalePriceStructure
            {
                Finishes = finishes,
                PriceOnProperty = inventory.PriceOnProperty,
                QuantityOnProperty = inventory.QuantityOnProperty,
                SkuOnProperty = inventory.SkuOnProperty,
                ReadinessStateOnProperty = inventory.ReadinessStateOnProperty
            };
        }

        public async Task<DraftListingTemplate> GetDraftListingTemplateAsync(
            long listingId,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);

            var endpoint = CreateAPIRoute($"application/listings/{listingId}");
            using var response = await _http.GetAsync(endpoint, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Etsy listing request failed ({(int)response.StatusCode}): {responseContent}");
            }

            var listing = JsonSerializer.Deserialize<EtsyListingTemplateResponse>(responseContent, EtsyJsonOptions)
                ?? throw new InvalidOperationException("Etsy returned an empty listing response.");
            if (listing.TaxonomyId <= 0 || string.IsNullOrWhiteSpace(listing.WhoMade) || string.IsNullOrWhiteSpace(listing.WhenMade))
                throw new InvalidOperationException("The source listing is missing metadata required to create a draft.");

            return new DraftListingTemplate
            {
                TaxonomyId = listing.TaxonomyId,
                WhoMade = listing.WhoMade,
                WhenMade = listing.WhenMade,
                IsSupply = listing.IsSupply,
                ListingType = string.IsNullOrWhiteSpace(listing.ListingType) ? "physical" : listing.ListingType,
                ShippingProfileId = listing.ShippingProfileId,
                ReturnPolicyId = listing.ReturnPolicyId,
                ShopSectionId = listing.ShopSectionId
            };
        }

        public async Task<CreatedDraftListing> CreateDraftListingAsync(
            DraftListingRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateDraftRequest(request);

            var resolvedPriceStructure = await ResolveProcessingTimesAsync(request.PriceStructure, cancellationToken);
            var readinessStateId = request.ReadinessStateId
                ?? resolvedPriceStructure.Finishes.FirstOrDefault()?.ReadinessStateId
                ?? throw new InvalidOperationException("A draft requires a readiness state ID or a finish processing time.");

            var activeScales = resolvedPriceStructure.Finishes
                .SelectMany(finish => finish.Scales)
                .Where(scale => scale.IsEnabled)
                .ToList();
            if (activeScales.Count == 0)
                throw new InvalidOperationException("A draft requires at least one enabled offering.");

            var formValues = new Dictionary<string, string>
            {
                ["quantity"] = activeScales.Sum(scale => scale.Quantity).ToString(CultureInfo.InvariantCulture),
                ["title"] = request.Title,
                ["description"] = request.Description,
                ["price"] = activeScales.Min(scale => scale.Price).ToString(CultureInfo.InvariantCulture),
                ["who_made"] = request.WhoMade,
                ["when_made"] = request.WhenMade,
                ["taxonomy_id"] = request.TaxonomyId.ToString(CultureInfo.InvariantCulture),
                ["is_supply"] = request.IsSupply.ToString().ToLowerInvariant(),
                ["type"] = request.ListingType,
                ["readiness_state_id"] = readinessStateId.ToString(CultureInfo.InvariantCulture)
            };

            AddOptionalFormValue(formValues, "shipping_profile_id", request.ShippingProfileId);
            AddOptionalFormValue(formValues, "return_policy_id", request.ReturnPolicyId);
            AddOptionalFormValue(formValues, "shop_section_id", request.ShopSectionId);

            var tags = NormalizeTags(request.Tags);
            if (tags.Count > 0)
                formValues["tags"] = string.Join(',', tags);

            var endpoint = CreateAPIRoute("application/shops/{_shopId}/listings?legacy=false");
            using var response = await _http.PostAsync(endpoint, new FormUrlEncodedContent(formValues), cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Etsy draft creation failed ({(int)response.StatusCode}): {responseContent}");
            }

            var createdListing = JsonSerializer.Deserialize<EtsyCreatedListingResponse>(responseContent, EtsyJsonOptions)
                ?? throw new InvalidOperationException("Etsy returned an empty draft creation response.");
            if (createdListing.ListingId <= 0)
                throw new InvalidOperationException("Etsy did not return a listing ID for the created draft.");

            try
            {
                await UpdateListingInventoryAsync(
                    createdListing.ListingId,
                    resolvedPriceStructure.ForNewListing(),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Draft {createdListing.ListingId} was created, but its variation inventory was not applied. " +
                    "The draft remains in Etsy and can be completed manually.",
                    exception);
            }

            return new CreatedDraftListing
            {
                ListingId = createdListing.ListingId,
                State = createdListing.State,
                Url = createdListing.Url
            };
        }

        public async Task<string> UpdateListingTagsAsync(
            long listingId,
            IEnumerable<string> tags,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
            ArgumentNullException.ThrowIfNull(tags);

            var normalizedTags = NormalizeTags(tags);
            var endpoint = CreateAPIRoute($"application/shops/{{_shopId}}/listings/{listingId}?legacy=false");
            using var response = await _http.PatchAsync(
                endpoint,
                new FormUrlEncodedContent(
                    new Dictionary<string, string> { ["tags"] = string.Join(',', normalizedTags) }),
                cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Etsy tag update failed ({(int)response.StatusCode}): {responseContent}");
            }

            return responseContent;
        }

        public async Task<UploadedListingImage> UploadListingImageAsync(
            long listingId,
            string imagePath,
            int rank,
            string? altText = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rank);
            ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("The listing image file was not found.", imagePath);

            await using var imageStream = File.OpenRead(imagePath);
            using var imageContent = new StreamContent(imageStream);
            using var form = new MultipartFormDataContent();
            form.Add(imageContent, "image", Path.GetFileName(imagePath));
            form.Add(new StringContent(rank.ToString(CultureInfo.InvariantCulture)), "rank");

            if (!string.IsNullOrWhiteSpace(altText))
                form.Add(new StringContent(altText), "alt_text");

            var endpoint = CreateAPIRoute($"application/shops/{{_shopId}}/listings/{listingId}/images");
            using var response = await _http.PostAsync(endpoint, form, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Etsy image upload failed ({(int)response.StatusCode}): {responseContent}");
            }

            var uploadedImage = JsonSerializer.Deserialize<EtsyListingImageUploadResponse>(responseContent, EtsyJsonOptions)
                ?? throw new InvalidOperationException("Etsy returned an empty image upload response.");
            if (uploadedImage.ListingImageId <= 0)
                throw new InvalidOperationException("Etsy did not return a listing image ID after upload.");

            return new UploadedListingImage
            {
                ListingId = uploadedImage.ListingId,
                ListingImageId = uploadedImage.ListingImageId,
                Rank = uploadedImage.Rank,
                FullImageUrl = uploadedImage.FullImageUrl
            };
        }

        private string CreateAPIRoute(string endpoint)
        {
            var route = endpoint.Replace("{_shopId}", _shopId);
            return $"https://openapi.etsy.com/v3/{route}";
        }

        private static void ValidateDraftRequest(DraftListingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new InvalidOperationException("A draft title is required.");
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new InvalidOperationException("A draft description is required.");
            if (request.TaxonomyId <= 0)
                throw new InvalidOperationException("A draft taxonomy ID is required.");
            if (string.IsNullOrWhiteSpace(request.WhoMade))
                throw new InvalidOperationException("A draft who_made value is required.");
            if (string.IsNullOrWhiteSpace(request.WhenMade))
                throw new InvalidOperationException("A draft when_made value is required.");
        }

        private async Task<FinishScalePriceStructure> ResolveProcessingTimesAsync(
            FinishScalePriceStructure priceStructure,
            CancellationToken cancellationToken)
        {
            var finishes = new List<FinishPricing>();
            foreach (var finish in priceStructure.Finishes)
            {
                var readinessStateId = finish.ReadinessStateId
                    ?? (finish.ProcessingTime is { } processingTime
                        ? await GetOrCreateReadinessStateIdAsync(processingTime, cancellationToken)
                        : throw new InvalidOperationException(
                            $"Finish '{finish.Name}' needs either a readiness state ID or a processing time."));

                finishes.Add(new FinishPricing
                {
                    Name = finish.Name,
                    ValueId = finish.ValueId,
                    ReadinessStateId = readinessStateId,
                    ProcessingTime = finish.ProcessingTime,
                    Scales = finish.Scales
                });
            }

            return new FinishScalePriceStructure
            {
                Finishes = finishes,
                PriceOnProperty = priceStructure.PriceOnProperty,
                QuantityOnProperty = priceStructure.QuantityOnProperty,
                SkuOnProperty = priceStructure.SkuOnProperty,
                ReadinessStateOnProperty = priceStructure.ReadinessStateOnProperty
            };
        }

        private async Task<long> GetOrCreateReadinessStateIdAsync(
            ProcessingTime processingTime,
            CancellationToken cancellationToken)
        {
            ValidateProcessingTime(processingTime);

            var formValues = new Dictionary<string, string>
            {
                ["readiness_state"] = "made_to_order",
                ["min_processing_time"] = processingTime.Minimum.ToString(CultureInfo.InvariantCulture),
                ["max_processing_time"] = processingTime.Maximum.ToString(CultureInfo.InvariantCulture),
                ["processing_time_unit"] = processingTime.Unit == ProcessingTimeUnit.Days ? "days" : "weeks"
            };

            var endpoint = CreateAPIRoute("application/shops/{_shopId}/readiness-state-definitions");
            using var response = await _http.PostAsync(
                endpoint,
                new FormUrlEncodedContent(formValues),
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var definition = await response.Content.ReadFromJsonAsync<EtsyReadinessStateDefinition>(EtsyJsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException("Etsy returned an empty readiness-state definition.");
                if (definition.ReadinessStateId <= 0)
                    throw new InvalidOperationException("Etsy did not return a readiness-state ID.");

                return definition.ReadinessStateId;
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var existingDefinitionUri = response.Content.Headers.ContentLocation;
                if (existingDefinitionUri is not null)
                {
                    var existingDefinitionEndpoint = existingDefinitionUri.IsAbsoluteUri
                        ? existingDefinitionUri
                        : new Uri(CreateAPIRoute(existingDefinitionUri.OriginalString));
                    using var existingResponse = await _http.GetAsync(existingDefinitionEndpoint, cancellationToken);
                    existingResponse.EnsureSuccessStatusCode();
                    var existingDefinition = await existingResponse.Content.ReadFromJsonAsync<EtsyReadinessStateDefinition>(EtsyJsonOptions, cancellationToken)
                        ?? throw new InvalidOperationException("Etsy returned an empty existing readiness-state definition.");
                    if (existingDefinition.ReadinessStateId > 0)
                        return existingDefinition.ReadinessStateId;
                }
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Etsy readiness-state creation failed ({(int)response.StatusCode}): {responseContent}");
        }

        private static void ValidateProcessingTime(ProcessingTime processingTime)
        {
            if (processingTime.Minimum is < 1 or > 10 || processingTime.Maximum is < 1 or > 10)
                throw new InvalidOperationException("Etsy processing-time values must be between 1 and 10.");
            if (processingTime.Minimum > processingTime.Maximum)
                throw new InvalidOperationException("The minimum processing time cannot exceed the maximum.");
        }

        private static void AddOptionalFormValue(Dictionary<string, string> formValues, string name, long? value)
        {
            if (value is > 0)
                formValues[name] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            var normalizedTags = tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedTags.Any(tag => tag.Contains(',')))
                throw new InvalidOperationException("Etsy tags cannot contain commas because Etsy receives tags as a comma-separated list.");

            return normalizedTags;
        }

        private async Task<string> ResolveShopIdAsync(string accessToken)
        {
            var separatorIndex = accessToken.IndexOf('.');
            if (separatorIndex <= 0 || !long.TryParse(accessToken[..separatorIndex], out var userId))
            {
                throw new InvalidOperationException(
                    "The Etsy access token did not contain the expected numeric user ID prefix.");
            }

            var response = await _http.GetAsync(
                $"https://openapi.etsy.com/v3/application/users/{userId}/shops");
            response.EnsureSuccessStatusCode();

            var shop = await response.Content.ReadFromJsonAsync<Shop>();
            if (shop is null || shop.ShopId <= 0)
                throw new InvalidOperationException("Etsy did not return a shop ID for the authenticated user.");

            Console.WriteLine($"Authenticated Etsy shop: {shop.ShopName} ({shop.ShopId})");
            return shop.ShopId.ToString();
        }

        private sealed class Shop
        {
            public long ShopId { get; init; }

            public string ShopName { get; init; } = string.Empty;
        }

        private static EtsyInventoryPropertyValue GetPropertyValue(
            EtsyInventoryProductResponse product,
            long propertyId,
            string propertyName) =>
            product.PropertyValues.SingleOrDefault(value => value.PropertyId == propertyId)
                ?? throw new InvalidOperationException(
                    $"Product {product.ProductId} does not have the required {propertyName} variation.");

        private static string GetSingleValue(EtsyInventoryPropertyValue property, long productId) =>
            property.Values.FirstOrDefault() is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"Product {productId} has no value for variation property {property.PropertyId}.");

        private sealed class EtsyInventoryResponse
        {
            public List<EtsyInventoryProductResponse> Products { get; init; } = [];

            public List<long> PriceOnProperty { get; init; } = [];

            public List<long> QuantityOnProperty { get; init; } = [];

            public List<long> SkuOnProperty { get; init; } = [];

            public List<long> ReadinessStateOnProperty { get; init; } = [];
        }

        private sealed class EtsyListingTemplateResponse
        {
            public long TaxonomyId { get; init; }

            public string WhoMade { get; init; } = string.Empty;

            public string WhenMade { get; init; } = string.Empty;

            public bool IsSupply { get; init; }

            public string ListingType { get; init; } = string.Empty;

            public long? ShippingProfileId { get; init; }

            public long? ReturnPolicyId { get; init; }

            public long? ShopSectionId { get; init; }
        }

        private sealed class EtsyCreatedListingResponse
        {
            public long ListingId { get; init; }

            public string State { get; init; } = string.Empty;

            public string Url { get; init; } = string.Empty;
        }

        private sealed class EtsyReadinessStateDefinition
        {
            public long ReadinessStateId { get; init; }
        }

        private sealed class EtsyListingImageUploadResponse
        {
            public long ListingId { get; init; }

            public long ListingImageId { get; init; }

            public int Rank { get; init; }

            [JsonPropertyName("url_fullxfull")]
            public string FullImageUrl { get; init; } = string.Empty;
        }

        private sealed class EtsyInventoryProductResponse
        {
            public long ProductId { get; init; }

            public string Sku { get; init; } = string.Empty;

            public bool IsDeleted { get; init; }

            public List<EtsyInventoryOfferingResponse> Offerings { get; init; } = [];

            public List<EtsyInventoryPropertyValue> PropertyValues { get; init; } = [];
        }

        private sealed class EtsyInventoryOfferingResponse
        {
            public long OfferingId { get; init; }

            public int Quantity { get; init; }

            public bool IsEnabled { get; init; }

            public bool IsDeleted { get; init; }

            public required EtsyMoney Price { get; init; }

            public long ReadinessStateId { get; init; }
        }

        private sealed class EtsyMoney
        {
            public long Amount { get; init; }

            public long Divisor { get; init; }
        }

        private sealed class EtsyInventoryPropertyValue
        {
            public long PropertyId { get; init; }

            public List<long> ValueIds { get; init; } = [];

            public List<string> Values { get; init; } = [];
        }
    }
}
