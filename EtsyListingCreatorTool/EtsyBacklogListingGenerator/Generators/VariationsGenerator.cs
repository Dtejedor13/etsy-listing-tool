using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EtsyBacklogListingGenerator.Generators
{
    internal class VariationsGenerator
    {
        private ScaleCalculator scaleCalculator = new ScaleCalculator();
        private Dictionary<int, int> priceList = new Dictionary<int, int>()
        {
            { 10, 25 },
            { 8, 45 },
            { 6, 79 }
        };

        public string GenerateVariationsString(JsonNode listingInfo)
        {
            var defaultScale = Convert.ToInt16(listingInfo["default_scale"]!.ToString());
            var defaultSize = Convert.ToDouble(listingInfo["original_size"]!.ToString());
            var scaleOptions = listingInfo["scales"]!.AsArray();
            var sizes = new List<KeyValuePair<string, int>>();
            var scaleFrom = scaleCalculator.TranslateToScale(defaultScale);
            foreach (var scaleOption in scaleOptions)
            {
                var scaleOpt = Convert.ToInt16(scaleOption!.ToString());
                var scaledSize = scaleCalculator.Convert(defaultSize, scaleFrom, scaleCalculator.TranslateToScale(scaleOpt));
                var roudedSize = Math.Round(scaledSize, 0);
                sizes.Add(new KeyValuePair<string, int>($"DIY ({roudedSize} cm)", priceList[scaleOpt]));
                sizes.Add(new KeyValuePair<string, int>($"Polished ({roudedSize} cm)", priceList[scaleOpt] + 30));
            }
            sizes.Add(new KeyValuePair<string, int>("For Painted DM me!", 200));

            return FormatAndSortSizes(sizes);
        }


        private string FormatAndSortSizes(List<KeyValuePair<string, int>> sizes)
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
                .Where(s => s.Key.Contains("DM"));

            var ordered = diy
                .Concat(polished)
                .Concat(dm);

            return string.Join("\n", ordered.Select(s => $"- {s.Key} {s.Value}"));
        }
    }
}
