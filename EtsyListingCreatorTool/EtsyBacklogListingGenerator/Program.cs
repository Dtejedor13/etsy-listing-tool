using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EtsyBacklogListingGenerator;
using EtsyBacklogListingGenerator.AI;

Dictionary<int, int> priceList = new Dictionary<int, int>()
{
    { 10, 25 },
    { 8, 45 },
    { 6, 79 }
};

var aiManager = new OpenAIManager();
var tagsGenerator = new TagsGenerator(aiManager);
var descriptionGenerator = new DescriptionGenerator(aiManager);
var scaleCalculator = new ScaleCalculator();

foreach (var directory in Directory.GetDirectories("F:\\Etsy Shop\\Backlog"))
{
    var directoryName = directory.Split('.')[0].Split("\\").Last();
    if (directoryName.StartsWith("_"))
        continue;

    if (!File.Exists($"{directory}/images/finish_types_v3.png"))
        File.Copy("F:\\Etsy Shop\\docs\\finish_types_v3.png", $"{directory}/images/finish_types_v3.png");
    if (!File.Exists($"{directory}/images/Painted_commision.png"))
        File.Copy("F:\\Etsy Shop\\docs\\Painted_commision.png", $"{directory}/images/Painted_commision.png");

    var listingImages = Directory.GetFiles($"{directory}/images");
    var listingInfo = GetInfo(directory);
    
    var availableScales = string.Empty;
    var defaultScale = Convert.ToInt16(listingInfo["default_scale"]!.ToString());
    var defaultSize = Convert.ToDouble(listingInfo["original_size"]!.ToString());

    var scaleOptions = listingInfo["scales"]!.AsArray();
    var scaleFrom = scaleCalculator.TranslateToScale(defaultScale);
    var sizes = new List<KeyValuePair<string, int>>();
    foreach (var scaleOption in scaleOptions)
    {
        var scaleOpt = Convert.ToInt16(scaleOption!.ToString());
        var scaledSize = scaleCalculator.Convert(defaultSize, scaleFrom, scaleCalculator.TranslateToScale(scaleOpt));
        var roudedSize = Math.Round(scaledSize, 0);
        sizes.Add(new KeyValuePair<string, int>($"DIY ({roudedSize} cm)", priceList[scaleOpt]));
        sizes.Add(new KeyValuePair<string, int>($"Polished ({roudedSize} cm)", priceList[scaleOpt] + 30));
    }
    sizes.Add(new KeyValuePair<string, int>("For Painted DM me!", 200));

    var additionalInfo = listingInfo["additional_infos"]?.ToString() ?? string.Empty;

    foreach (var scaleOption in scaleOptions)
    {
        if (string.IsNullOrEmpty(availableScales))
            availableScales += $" 1/{scaleOption}";
        else
            availableScales += $", 1/{scaleOption}";
    }

    var characterPrompt = $"{listingInfo["name"]} from {listingInfo["universe"]} available scales are {availableScales}, additional infos: {additionalInfo}";

    var title = $"{listingInfo["name"]} Inspired Resin Figure Fan Art";
    var description = await descriptionGenerator.GenerateDescriptionAsync(characterPrompt, listingInfo["creator"]!.ToString());

    var tags = await tagsGenerator.GenerateTagsAsync(characterPrompt);

    using (var stream = new FileStream($"{directory}/listing.txt", FileMode.OpenOrCreate, FileAccess.Write))
    using (var writer = new StreamWriter(stream))
    {
        string content = $"{$"{directory}/images"}\n\n{title}\n\n{description}\n\n{FormatAndSortSizes(sizes)}\n\n{tags}";
        writer.Write(content);
    }

    Console.WriteLine($"generation done for {directory}");
}

JsonNode GetInfo(string basePath)
{
    using (var stream = new FileStream($"{basePath}/info.json", FileMode.Open, FileAccess.Read))
    using (var reader = new StreamReader(stream))
    {
        var json = reader.ReadToEnd();
        var node = JsonNode.Parse(json);
        return node!;
    }
}

string FormatAndSortSizes(List<KeyValuePair<string, int>> sizes)
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