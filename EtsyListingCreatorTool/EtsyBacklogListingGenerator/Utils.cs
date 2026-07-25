using System.Text.Json.Nodes;

namespace EtsyBacklogListingGenerator
{
    public static class Utils
    {
        public static string CreateCharacterPrompt(JsonNode listingInfo)
        {
            var scaleOptions = listingInfo["scales"]!.AsArray();
            var additionalInfo = listingInfo["additional_infos"]?.ToString() ?? string.Empty;
            return $"{listingInfo["name"]} from {listingInfo["universe"]} available scales are {GetAvailibleScalesString(scaleOptions)}, additional infos: {additionalInfo}";
        }

        public static string GetAvailibleScalesString(JsonArray scales)
        {
            var availableScales = string.Empty;

            foreach (var scaleOption in scales)
            {
                if (string.IsNullOrEmpty(availableScales))
                    availableScales += $" 1/{scaleOption}";
                else
                    availableScales += $", 1/{scaleOption}";
            }
            return availableScales;
        }
    }
}
