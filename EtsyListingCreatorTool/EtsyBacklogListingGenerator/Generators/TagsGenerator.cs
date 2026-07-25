using EtsyBacklogListingGenerator.AI;

namespace EtsyBacklogListingGenerator.Generators
{
    public class TagsGenerator(OpenAIManager aiManager)
    {
        public async Task<string> GenerateTagsAsync(string characterPrompt)
        {
            return await aiManager.AskAsync($"tags for {characterPrompt}, do not generate any tags related to the scale i.e 1 to 10 scale figure");
        }
    }
}
