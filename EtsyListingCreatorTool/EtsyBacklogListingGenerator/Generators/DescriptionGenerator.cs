using EtsyBacklogListingGenerator.AI;


namespace EtsyBacklogListingGenerator.Generators
{
    internal class DescriptionGenerator(OpenAIManager aiManager) : AISupportedGenerator
    {
        private Dictionary<string, string> CreatorInfoMap = new Dictionary<string, string>()
        {
            { "samiho", @"""This model was sculpted by Samiho Studios.
All prints are produced under an official commercial license, with full respect for the original artist’s work.

Supporting talented creators is an important part of this project.
Patreon: https://www.patreon.com/c/samihostudios"""},

            { "lucas", @"""This model was sculpted by LUCAS PEREZ.
All prints are produced under an official commercial license, with full respect for the original artist’s work.

Supporting talented creators is an important part of this project.
Patreon: https://www.patreon.com/cw/LucasPereZ
"""},

            { "kaidan", @"""This model was sculpted by Kaidan3D.
All prints are produced under an official commercial license, with full respect for the original artist’s work.

Supporting talented creators is an important part of this project.
Patreon: https://www.patreon.com/cw/kaidan3d""" },

            { "myanimate", @"""This model was sculpted by myAnimate
All prints are produced under an official commercial license, with full respect for the original artist’s work.

Supporting talented creators is an important part of this project.
Patreon: https://www.patreon.com/cw/myanimate"""}
        };

        private const string descriptionSystemPrompt = @"private const string descriptionSystemPrompt = @""
        Create an Etsy listing DESCRIPTION.

        DATA INPUT:
        Character: {name}
        Universe: {universe}
        Available sizes and prices:
        {sizes}

        Creator information (MUST be included exactly as written, do NOT rewrite):
        {creator}

        IMPORTANT:
        - The creator is the sculptor of the model, NOT the character
        - NEVER describe the creator as a character

        CRITICAL RULES:
        - ONLY use the provided character and universe
        - NEVER mention other anime, franchises, or characters
        - NEVER invent names or replace the character

        SEO INTRO (MANDATORY):
        Write 2–3 sentences including:
        - character name
        - universe
        - """"resin figure""""
        - """"collectible"""" or """"statue""""
        - mention availability (DIY / Polished / Painted)

        Example style:
        High-quality {name} resin figure from {universe}.
        This fan art collectible statue is available as a DIY kit, polished version, or painted option—perfect for collectors and display setups.

        CHARACTER DESCRIPTION:
        - Write a cinematic paragraph about THIS character
        - Include 1 relevant keyword if applicable (e.g. Akatsuki, Saiyan, Pirate)
        - Keep it accurate to the character

        STRUCTURE:
        1. SEO Intro
        2. Character Description
        3. Product Details
        4. Finish Options (DIY / Polished / Painted)
        5. Artist Credit (insert creator text EXACTLY)

        DO NOT:
        - mix universes
        - invent keywords
        - hallucinate brand names
        - repeat keywords unnaturally

        STYLE:
        - clean formatting
        - natural language
        - no emoji spam
        - no generic filler text

        OUTPUT:
        Return clean formatted listing text only.
        "";";

        public async Task<string> GenerateDescriptionAsync(string name, string universe, string sizes, string creator)
        {
            var filledPrompt = descriptionSystemPrompt
            .Replace("{name}", name)
            .Replace("{universe}", universe)
            .Replace("{sizes}", sizes)
            .Replace("{creator}", CreatorInfoMap[creator]);

            var prompt = $"{GenericSystemPrompt}\n\n{filledPrompt}";
            return await aiManager.AskAsync(prompt);
        }
    }
}
