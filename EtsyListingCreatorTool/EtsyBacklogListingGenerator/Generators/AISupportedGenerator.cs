using EtsyBacklogListingGenerator.AI;

namespace EtsyBacklogListingGenerator.Generators
{
    abstract class AISupportedGenerator()
    {
        protected string GenericSystemPrompt = @"You are an expert Etsy SEO copywriter specialized in collectible resin figures and fan art statues.
        Write natural, high-converting listings optimized for Etsy search.
        Avoid keyword stuffing. Keep the tone human, appealing to collectors and hobbyists.";
    }
}
