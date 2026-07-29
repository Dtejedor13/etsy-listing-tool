using System;
using System.Collections.Generic;
using System.Text;
using EtsyBacklogListingGenerator.Drafts;
using EtsyBacklogListingGenerator.Inventory;

namespace EtsyBacklogListingGenerator.samples
{
    public class GenerateDraft
    {
        public async Task Main()
        {
            const long FiguresCategoryId = 130;

            var etsyClient = new EtsyClient();
            await etsyClient.AuthenticateAsync();
            const long madeToOrderProfileId = 1454550495051; // Your Etsy readiness-state ID
            var customPrices = new FinishScalePriceStructure
            {
                Finishes =
                [
                    new FinishPricing
        {
            Name = "DIY Unpainted",
            ReadinessStateId = madeToOrderProfileId,
            Scales =
            [
                new ScalePricing { Name = "1/10 30 cm", Price = 30.00m, Quantity = 10 },
                new ScalePricing { Name = "1/8 42 cm", Price = 50.00m, Quantity = 10 },
                new ScalePricing { Name = "1/6 60 cm", Price = 85.00m, Quantity = 10 }
            ]
        },
        new FinishPricing
        {
            Name = "Polished Unpainted",
            ReadinessStateId = madeToOrderProfileId,
            Scales =
            [
                new ScalePricing { Name = "1/10 30 cm", Price = 60.00m, Quantity = 10 },
                new ScalePricing { Name = "1/8 42 cm", Price = 80.00m, Quantity = 10 },
                new ScalePricing { Name = "1/6 60 cm", Price = 115.00m, Quantity = 10 }
            ]
        }
                ]
            };

            var request = new DraftListingRequest
            {
                Title = "Draft Test",
                Description = "this is just a test if my software is working",
                TaxonomyId = FiguresCategoryId,
                WhoMade = "i_did",
                WhenMade = "made_to_order",
                IsSupply = false,
                ListingType = "physical",
                ReadinessStateId = madeToOrderProfileId,
                PriceStructure = customPrices,
                ShippingProfileId = 308769271800, // dhl europe
                ReturnPolicyId = 1453676753414,
                Tags = new List<string>() { "test-tag-1", "test-tag-2" }
            };
            var draft = await etsyClient.CreateDraftListingAsync(request);
            await etsyClient.UploadListingImageAsync(
                draft.ListingId,
                @"F:\Etsy Shop\Backlog\Rimuru & Ranga\images\1 (1).jpg",
                rank: 1,
                altText: "Unpainted resin figure");

            //const long sampleDraftId = 4546285619;
            //var priceStructure = await etsyClient.GetPriceStructureFromListingAsync(sampleDraftId);
            //Console.WriteLine($"Verified draft {sampleDraftId}: {priceStructure.Finishes.Count} finishes and " +
            //                  $"{priceStructure.Finishes.Sum(finish => finish.Scales.Count)} Finish/Scale offerings.");
        }
    }
}
