using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EtsyBacklogListingGenerator.Inventory;

namespace EtsyBacklogListingGenerator.Generators
{
    public class VariationsGenerator
    {
        private ScaleCalculator scaleCalculator = new ScaleCalculator();

        private Dictionary<string, int> pointsMap = new Dictionary<string, int>()
        {
            { "big base",  1 },
            { "effects (clear parts)", 1 },
            { "big wings", 1 },
            { "big wepons (greater then figure)", 1 },
            { "20+ parts", 2 },
            { "40+ parts", 2 },
            { "multiple figures", 2 },
            { "diorama", 2 }
        };

        private Dictionary<int, double> priceListA = new Dictionary<int, double>()
        {
            { 10, 29.99 },
            { 8, 49.99 },
            { 6, 84.99 }
        };

        private Dictionary<int, double> priceListB = new Dictionary<int, double>() // ToDo: add class tags and define prices for b a and s where b are very little models and s realy heavy big dioramas
        {
            { 10, 39.99 },
            { 8, 59.99 },
            { 6, 94.99 }
        };

        private Dictionary<int, double> priceListC = new Dictionary<int, double>() // ToDo: add class tags and define prices for b a and s where b are very little models and s realy heavy big dioramas
        {
            { 10, 54.99 },
            { 8, 74.99 },
            { 6, 109.99 }
        };

        private Dictionary<int, double> polishePrices = new Dictionary<int, double>()
        {
            { 10, 35.00 },
            { 8, 45.00 },
            { 6, 60.00 }
        };

        private double paintedPrice = 200;

        public FinishScalePriceStructure GenerateFigurePriceStructure(JsonNode listingInfo)
        {
            var defaultScale = Convert.ToInt16(listingInfo["default_scale"]!.ToString());
            var defaultSize = Convert.ToDouble(listingInfo["original_size"]!.ToString());
            var scaleOptions = listingInfo["scales"]!.AsArray();
            var sizes = new List<KeyValuePair<string, double>>();
            var scaleFrom = scaleCalculator.TranslateToScale(defaultScale);
            var figureTier = calculatePointsAndDefinePriceList(listingInfo);

            var processingTimes = new FigureFinishProcessingTimes
            {
                DiyUnpainted = new ProcessingTime { 
                    Minimum = figureTier.Tier != 3? 4 : 7, 
                    Maximum = 10 
                },
                PolishedUnpainted = new ProcessingTime
                {
                    Minimum = 2,
                    Maximum = 2,
                    Unit = ProcessingTimeUnit.Weeks
                },
                Painted = new ProcessingTime
                {
                    Minimum = 4,
                    Maximum = 6,
                    Unit = ProcessingTimeUnit.Weeks
                }
            };


            var structure = new FinishScalePriceStructure();
            structure.Finishes.Add(new FinishPricing
            {
                Name = "DIY Unpainted",
                ProcessingTime = processingTimes.DiyUnpainted,
                Scales = new List<ScalePricing>()
            });
            structure.Finishes.Add(new FinishPricing
            {
                Name = "Polished Unpainted",
                ProcessingTime = processingTimes.PolishedUnpainted,
                Scales = new List<ScalePricing>()
            });
            structure.Finishes.Add(new FinishPricing
            {
                Name = "Painted(DM me!)",
                ProcessingTime = processingTimes.Painted,
                Scales = new List<ScalePricing>()
            });

           
            foreach (var scaleOption in scaleOptions.OrderByDescending(x => x!.GetValue<int>()))
            {
                var scaleOpt = Convert.ToInt16(scaleOption!.ToString());
                var scaledSize = scaleCalculator.Convert(defaultSize, scaleFrom, scaleCalculator.TranslateToScale(scaleOpt));
                var roudedSize = Math.Round(scaledSize, 0);

                foreach (var finishStruture in structure.Finishes)
                {
                    finishStruture.Scales.Add(new ScalePricing
                    {
                        Name = $"1/{scaleOption} {roudedSize} cm",
                        Price = Convert.ToDecimal(
                            finishStruture.Name == "DIY Unpainted" ? figureTier.PriceList[scaleOpt] :
                            finishStruture.Name == "Polished Unpainted" ? figureTier.PriceList[scaleOpt] + polishePrices[scaleOpt] :
                            figureTier.PriceList[scaleOpt] + polishePrices[scaleOpt] + paintedPrice
                            )
                    });
                }
            }
            return structure;
        }

        public string GenerateVariationsString(JsonNode listingInfo)
        {
            var defaultScale = Convert.ToInt16(listingInfo["default_scale"]!.ToString());
            var defaultSize = Convert.ToDouble(listingInfo["original_size"]!.ToString());
            var scaleOptions = listingInfo["scales"]!.AsArray();
            var sizes = new List<KeyValuePair<string, double>>();
            var scaleFrom = scaleCalculator.TranslateToScale(defaultScale);
            var figureTier = calculatePointsAndDefinePriceList(listingInfo);

            foreach (var scaleOption in scaleOptions)
            {
                var scaleOpt = Convert.ToInt16(scaleOption!.ToString());
                var scaledSize = scaleCalculator.Convert(defaultSize, scaleFrom, scaleCalculator.TranslateToScale(scaleOpt));
                var roudedSize = Math.Round(scaledSize, 0);
                sizes.Add(new KeyValuePair<string, double>($"DIY ({roudedSize} cm)", figureTier.PriceList[scaleOpt]));
                sizes.Add(new KeyValuePair<string, double>($"Polished ({roudedSize} cm)", figureTier.PriceList[scaleOpt] + polishePrices[scaleOpt]));
                sizes.Add(new KeyValuePair<string, double>($"Painted ({roudedSize} cm)", figureTier.PriceList[scaleOpt] + polishePrices[scaleOpt] + paintedPrice));
            }

            return FormatAndSortSizes(sizes);
        }


        private string FormatAndSortSizes(List<KeyValuePair<string, double>> sizes)
        {
            int ExtractSize(string key)
            {
                var match = Regex.Match(key, @"\((\d+)\s*cm\)");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            }

            var diy = sizes
                .Where(s => s.Key.StartsWith("DIY"))
                .OrderBy(s => ExtractSize(s.Key)); // ASC

            var polished = sizes
                .Where(s => s.Key.StartsWith("Polished"))
                .OrderBy(s => ExtractSize(s.Key)); // ASC

            var dm = sizes
                .Where(s => s.Key.Contains("Painted"))
                .OrderBy (s => ExtractSize(s.Key)); // ASC

            var ordered = diy
                .Concat(polished)
                .Concat(dm);

            return string.Join("\n", ordered.Select(s => $"- {s.Key} {Math.Truncate(s.Value*100) / 100}".Replace(".", ",")));
        }

        private struct FigureTier
        {
            public int Points;
            public int Tier;
            public Dictionary<int, double> PriceList;
        }

        private FigureTier calculatePointsAndDefinePriceList(JsonNode info)
        {
            var attributes = info["points"]!.Deserialize<Dictionary<string, int>>();
            var points = 0;
            foreach (var attribute in attributes)
            {
                if (attribute.Value == 1)
                    points += pointsMap[attribute.Key];
            }

            if (points < 3) // 0-2 -> standard figure
                return new FigureTier
                {
                    PriceList = priceListA,
                    Points = points,
                    Tier = 1
                };
            else if (points < 5) // 3-4 -> complex figure
                return new FigureTier
                {
                    PriceList = priceListB,
                    Tier = 2,
                    Points = points
                };
            else // 5-n -> big diorama
                return new FigureTier
                {
                    PriceList = priceListC,
                    Points = points,
                    Tier = 3
                };
        }
    }

    public sealed class FigureFinishProcessingTimes
    {
        public required ProcessingTime DiyUnpainted { get; init; }

        public required ProcessingTime PolishedUnpainted { get; init; }

        public required ProcessingTime Painted { get; init; }
    }
}
