using EtsyBacklogListingGenerator.AI;

namespace EtsyBacklogListingGenerator.Generators
{
    internal class TitleGenerator(OpenAIManager aIManager) : AISupportedGenerator
    {
        public async Task<string> GenerateTitleAsync(string name, string universe)
        {
            var prompt = $@"
            {GenericSystemPrompt}

            Create ONE Etsy listing title.

            Character name: {name}
            Universe: {universe}

            STRICT RULES:
            - Use ONLY the provided character and universe
            - NEVER replace or invent another franchise
            - Max 140 characters
            - SINGLE LINE only
            - Must include: Resin Figure and Fan Art
            - Must include the correct universe early
            - Natural readable title (no keyword stacking)

            SEO IMPROVEMENT:
            - Include 1 relevant character-specific keyword if applicable (e.g. Akatsuki for Kisame, Saiyan for DBZ)
            - Prioritize important keywords early in the title
            - Avoid redundant words (e.g. DIY Kit + Garage Kit together)

            VARIATION:
            - Do NOT copy the example exactly
            - Slightly vary wording and keyword order

            Example (do NOT copy exactly):
            {name} Resin Figure {universe} Fan Art Collectible Anime Statue DIY Kit Painted Option Gift
            ";
            return await aIManager.AskAsync(prompt);
        }
    }
}
