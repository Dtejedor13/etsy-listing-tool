using System.Text.Json.Nodes;
using EtsyBacklogListingGenerator.AI;
using EtsyBacklogListingGenerator.Generators;

var aiManager = new OpenAIManager();
var tagsGenerator = new TagsGenerator(aiManager);
var descriptionGenerator = new DescriptionGenerator(aiManager);
var variationsGenerator = new VariationsGenerator();

foreach (var directory in Directory.GetDirectories("F:\\Etsy Shop\\Backlog"))
{
    var directoryName = directory.Split('.')[0].Split("\\").Last();
    if (directoryName.StartsWith("_"))
        continue;

    // copy finishing type and painting commision pngs to the directory
    if (!File.Exists($"{directory}/images/finish_types_v3.png"))
        File.Copy("F:\\Etsy Shop\\docs\\finish_types_v3.png", $"{directory}/images/finish_types_v3.png");
    if (!File.Exists($"{directory}/images/Painted_commision.png"))
        File.Copy("F:\\Etsy Shop\\docs\\Painted_commision.png", $"{directory}/images/Painted_commision.png");

    // get vars
    //var listingImages = Directory.GetFiles($"{directory}/images");
    var listingInfo = GetInfo(directory);
   
    var characterPrompt = CreateCharacterPrompt(listingInfo);

    // call generators
    var description = await descriptionGenerator.GenerateDescriptionAsync(characterPrompt, listingInfo["creator"]!.ToString());
    var variationString = variationsGenerator.GenerateVariationsString(listingInfo);
    var tags = await tagsGenerator.GenerateTagsAsync(characterPrompt);

    // write result
    using (var stream = new FileStream($"{directory}/listing.txt", FileMode.OpenOrCreate, FileAccess.Write))
    using (var writer = new StreamWriter(stream))
    {
        var title = $"{listingInfo["name"]} Inspired Resin Figure Fan Art";
        string content = $"{$"{directory}/images"}\n\n{title}\n\n{description}\n\n{variationString}\n\n{tags}";
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

string CreateCharacterPrompt(JsonNode listingInfo)
{
    var availableScales = string.Empty;
    var scaleOptions = listingInfo["scales"]!.AsArray();
    var additionalInfo = listingInfo["additional_infos"]?.ToString() ?? string.Empty;

    foreach (var scaleOption in scaleOptions)
    {
        if (string.IsNullOrEmpty(availableScales))
            availableScales += $" 1/{scaleOption}";
        else
            availableScales += $", 1/{scaleOption}";
    }

    return $"{listingInfo["name"]} from {listingInfo["universe"]} available scales are {availableScales}, additional infos: {additionalInfo}";
}