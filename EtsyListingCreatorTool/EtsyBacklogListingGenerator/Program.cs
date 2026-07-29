using System.Text.Json;
using System.Text.Json.Nodes;
using EtsyBacklogListingGenerator;
using EtsyBacklogListingGenerator.Drafts;
using EtsyBacklogListingGenerator.Generators;
using EtsyBacklogListingGenerator.Inventory;

var aiManager = new OpenAIManager();
var tagsGenerator = new TagsGenerator(aiManager);
var descriptionGenerator = new DescriptionGenerator(aiManager);
var variationsGenerator = new VariationsGenerator();
var titleGenerator = new TitleGenerator(aiManager);

Console.WriteLine("Select mode c => create listing, u => update listing");
var mode = Console.ReadLine()!.ToLower();
Console.Clear();

switch (mode)
{
    case "u":
        //while (true)
        //{
        //    JsonNode info = new JsonObject();
        //    Console.WriteLine("enter character name (CaseSensitive!)");
        //    info["name"] = Console.ReadLine();
        //    Console.WriteLine("enter character universe (CaseSensitive!)");
        //    info["universe"] = Console.ReadLine();
        //    Console.WriteLine("enter additional infos");
        //    info["additional_infos"] = Console.ReadLine();
        //    Console.WriteLine("enter original size");
        //    info["original_size"] = Console.ReadLine();
        //    Console.WriteLine("enter default scale");
        //    info["default_scale"] = Convert.ToInt16(Console.ReadLine());
        //    Console.WriteLine("enter scale options");
        //    var optionString = Console.ReadLine();
        //    var options = optionString.Split(",");
        //    var scales = new List<int>();
        //    foreach (var option in options)
        //        scales.Add(Convert.ToInt16(option));
        //    var json = JsonSerializer.Serialize(scales);
        //    info["scales"] = JsonSerializer.Deserialize<JsonArray>(json);
        //    Console.WriteLine("enter creator");
        //    info["creator"] = Console.ReadLine();

        //    Console.WriteLine("\n\n" + await GeneratListingInfoAsync(info));

        //    Console.WriteLine("continue ? y/n");
        //    if (Console.ReadLine()!.ToLower() == "n")
        //        break;
        //}

        break;

    case "c":
        const long FiguresCategoryId = 130;
       
        var etsyClient = new EtsyClient();
        await etsyClient.AuthenticateAsync();
        foreach (var directory in Directory.GetDirectories("F:\\Etsy Shop\\Backlog"))
        {
            var directoryName = directory.Split('.')[0].Split("\\").Last();
            if (directoryName.StartsWith("_"))
                continue;

            // copy finishing type and painting commision pngs to the directory
            if (!File.Exists($"{directory}/images/finish_types_v3.png"))
                File.Copy("F:\\Etsy Shop\\docs\\finish_types_v3.png", $"{directory}/images/finish_types_v3.png");

            // get vars
            var listingInfo = GetInfo(directory); // reads the info.json file
            var characterPrompt = Utils.CreateCharacterPrompt(listingInfo);
            var characterName = listingInfo["name"]!.ToString();
            var characterUniverse = listingInfo["universe"]!.ToString();
            var scaleOptions = listingInfo["scales"]!.AsArray();
            var availibleScalesString = Utils.GetAvailibleScalesString(scaleOptions);

            var description = await descriptionGenerator.GenerateDescriptionAsync(characterName, characterUniverse, availibleScalesString, listingInfo["creator"]!.ToString());
            var variationString = variationsGenerator.GenerateVariationsString(listingInfo);
            var tags = await tagsGenerator.GenerateTagsAsync(characterPrompt);
            var title = await titleGenerator.GenerateTitleAsync(characterName, characterUniverse);

            var draft = await etsyClient.CreateDraftListingAsync(new DraftListingRequest
            {
                Description = description,
                Title = title,
                TaxonomyId = FiguresCategoryId,
                WhoMade = "i_did",
                WhenMade = "made_to_order",
                IsSupply = false,
                ListingType = "physical",
                ShippingProfileId = 308769271800, // dhl europe
                ReturnPolicyId = 1453676753414,
                PriceStructure = variationsGenerator.GenerateFigurePriceStructure(listingInfo),
                Tags = tags
            });

            var rank = 1;
            foreach (string file in Directory.GetFiles($"{directory}/images"))
            {
                Console.WriteLine($"Pushing {file} to draft...");
                await etsyClient.UploadListingImageAsync(draft.ListingId, file, rank);
                rank++;
            }

            Console.WriteLine($"generation done for {directory}");
            break;
        }
        break;

}

JsonNode GetInfo(string basePath)
{
    JsonNode node;

    using (var stream = new FileStream($"{basePath}/info.json", FileMode.Open, FileAccess.Read))
    using (var reader = new StreamReader(stream))
    {
        var json = reader.ReadToEnd();
        node = JsonNode.Parse(json);
    }

    using (var stream = new FileStream($"{basePath}/points.json", FileMode.Open, FileAccess.Read))
    using (var reader = new StreamReader(stream))
    {
        var json = reader.ReadToEnd();
        node["points"] = JsonNode.Parse(json);
    }

    return node!;
}
