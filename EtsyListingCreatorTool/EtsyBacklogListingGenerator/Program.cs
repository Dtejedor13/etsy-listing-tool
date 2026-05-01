using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EtsyBacklogListingGenerator.AI;
using EtsyBacklogListingGenerator.Generators;

var aiManager = new OpenAIManager();
var tagsGenerator = new TagsGenerator(aiManager);
var descriptionGenerator = new DescriptionGenerator(aiManager);
var variationsGenerator = new VariationsGenerator();
var titleGenerator = new TitleGenerator(aiManager);

Console.WriteLine("Select mode c => create listing, u => update listing");
var mode = Console.ReadLine()!.ToLower();


switch(mode)
{
    case "u":
        while (true)
        {
            Console.Clear();
            JsonNode info = new JsonObject();
            Console.WriteLine("enter character name (CaseSensitive!)");
            info["name"] = Console.ReadLine();
            Console.WriteLine("enter character universe (CaseSensitive!)");
            info["universe"] = Console.ReadLine();
            Console.WriteLine("enter additional infos");
            info["additional_infos"] = Console.ReadLine();
            Console.WriteLine("enter original size");
            info["original_size"] = Console.ReadLine();
            Console.WriteLine("enter default scale");
            info["default_scale"] = Convert.ToInt16(Console.ReadLine());
            Console.WriteLine("enter scale options");
            var optionString = Console.ReadLine();
            var options = optionString.Split(",");
            var scales = new List<int>();
            foreach (var option in options)
                scales.Add(Convert.ToInt16(option));
            var json = JsonSerializer.Serialize(scales);
            info["scales"] = JsonSerializer.Deserialize<JsonArray>(json);
            Console.WriteLine("enter creator");
            info["creator"] = Console.ReadLine();

            Console.WriteLine("\n\n" + await GeneratListingInfoAsync(info));

            Console.WriteLine("continue ? y/n");
            if (Console.ReadLine()!.ToLower() == "n")
                break;
        }

        break;

    case "c":
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
            var listingInfo = GetInfo(directory);
            var content = await GeneratListingInfoAsync(listingInfo);

            // write result
            using (var stream = new FileStream($"{directory}/listing.txt", FileMode.OpenOrCreate, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
            }

            Console.WriteLine($"generation done for {directory}");
        }
        break;

}

async Task<string> GeneratListingInfoAsync(JsonNode listingInfo)
{
    var characterPrompt = CreateCharacterPrompt(listingInfo);

    // call generators
    var characterName = listingInfo["name"]!.ToString();
    var characterUniverse = listingInfo["universe"]!.ToString();
    var scaleOptions = listingInfo["scales"]!.AsArray();
    var availibleScalesString = GetAvailibleScalesString(scaleOptions);
    var description = await descriptionGenerator.GenerateDescriptionAsync(characterName, characterUniverse, availibleScalesString, listingInfo["creator"]!.ToString());
    var variationString = variationsGenerator.GenerateVariationsString(listingInfo);
    var tags = await tagsGenerator.GenerateTagsAsync(characterPrompt);
    var title = await titleGenerator.GenerateTitleAsync(characterName, characterUniverse);

    return $"{title}\n\n{description}\n\n{variationString}\n\n{tags}";
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

string GetAvailibleScalesString(JsonArray scales)
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

string CreateCharacterPrompt(JsonNode listingInfo)
{
    var scaleOptions = listingInfo["scales"]!.AsArray();
    var additionalInfo = listingInfo["additional_infos"]?.ToString() ?? string.Empty;
    return $"{listingInfo["name"]} from {listingInfo["universe"]} available scales are {GetAvailibleScalesString(scaleOptions)}, additional infos: {additionalInfo}";
}