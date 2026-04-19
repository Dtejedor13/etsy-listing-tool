using EtsyBacklogListingGenerator.AI;


namespace EtsyBacklogListingGenerator
{
    internal class DescriptionGenerator(OpenAIManager aiManager)
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

        private const string template = """
            {header}

            ⚪ Available Finish Options
            You can choose the finish level that best fits your project:

            ⚪ DIY (Basic Print)
            Supports removed
            Washed & fully cured
            Quality checked
            All parts included (complete kit)
            Assembly instructions included (created by me)
            Resin handling & safety guide included (created by me)
            Ready for your own sanding, filling and painting
            Best for: hobbyists who want full control over the finishing process

            ⚪ Polished (Refined Print)
            Everything from DIY, plus:
            Support marks sanded
            Visible holes filled
            Smoother surface finish
            Ready for priming and painting
            Best for: painters who want to save preparation time

            ⚪ Painted (On Request – Limited Slots)
            Painting is offered only when slots are available and must be discussed before purchase.
            Two painting levels are available:
            Each painted figure is a unique, handcrafted piece.

            ⚪ Painting Portfolio
            You can view examples of my painting quality and previous projects here:
            Instagram: https://www.instagram.com/denis_n_tejedor

            This allows you to clearly see my painting style and skill level before requesting a painted commission.

            ⚪ Artist Credit & Licensing
            {creator}

            ⚪ Shipping
            All orders are shipped via DHL with tracking and insurance.
            Each order is carefully packaged for safe delivery.

            Germany: 6.49 €
            International: 19.99 €

            If your actual shipping cost is lower, I will refund the difference.
            """;

        public async Task<string> GenerateDescriptionAsync(string characterPrompt, string creator)
        {
            var header = await aiManager.AskAsync($"description for {characterPrompt}");
            var description = template;
            return description.Replace("{header}", header).Replace("{creator}", CreatorInfoMap[creator]);
        }
    }
}
