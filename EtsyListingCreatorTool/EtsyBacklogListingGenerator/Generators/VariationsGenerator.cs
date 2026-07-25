using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

        public string GenerateVariationsString(JsonNode listingInfo)
        {
            var defaultScale = Convert.ToInt16(listingInfo["default_scale"]!.ToString());
            var defaultSize = Convert.ToDouble(listingInfo["original_size"]!.ToString());
            var scaleOptions = listingInfo["scales"]!.AsArray();
            var sizes = new List<KeyValuePair<string, double>>();
            var scaleFrom = scaleCalculator.TranslateToScale(defaultScale);
            var priceList = calculatePointsAndDefinePriceList(listingInfo);

            foreach (var scaleOption in scaleOptions)
            {
                var scaleOpt = Convert.ToInt16(scaleOption!.ToString());
                var scaledSize = scaleCalculator.Convert(defaultSize, scaleFrom, scaleCalculator.TranslateToScale(scaleOpt));
                var roudedSize = Math.Round(scaledSize, 0);
                sizes.Add(new KeyValuePair<string, double>($"DIY ({roudedSize} cm)", priceList[scaleOpt]));
                sizes.Add(new KeyValuePair<string, double>($"Polished ({roudedSize} cm)", priceList[scaleOpt] + polishePrices[scaleOpt]));
                sizes.Add(new KeyValuePair<string, double>($"Painted ({roudedSize} cm)", priceList[scaleOpt] + polishePrices[scaleOpt] + paintedPrice));
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

        private Dictionary<int, double> calculatePointsAndDefinePriceList(JsonNode info)
        {
            var attributes = info["points"]!.Deserialize<Dictionary<string, int>>();
            var points = 0;
            foreach (var attribute in attributes)
            {
                if (attribute.Value == 1)
                    points += pointsMap[attribute.Key];
            }

            if (points < 3) // 0-2 -> standard figure
                return priceListA;
            else if (points < 5) // 3-4 -> complex figure
                return priceListB;
            else // 5-n -> big diorama
                return priceListC;
        }
    }
}
