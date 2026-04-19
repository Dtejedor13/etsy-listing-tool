using EtsyBacklogListingGenerator.AI;

namespace EtsyBacklogListingGenerator.Generators
{
    internal class TagsGenerator(OpenAIManager aiManager)
    {
        public async Task<string> GenerateTagsAsync(string characterPrompt)
        {
            return await aiManager.AskAsync($"tags for {characterPrompt}");
        }
    }
}
