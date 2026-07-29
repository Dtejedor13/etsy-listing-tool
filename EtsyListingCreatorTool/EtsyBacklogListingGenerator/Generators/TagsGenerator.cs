
using System.Text.RegularExpressions;

namespace EtsyBacklogListingGenerator.Generators
{
    public class TagsGenerator(OpenAIManager aiManager)
    {
        public async Task<List<string>> GenerateTagsAsync(string characterPrompt)
        {
            var tagstring = await aiManager.AskAsync($"tags for {characterPrompt}, do not generate any tags related to the scale i.e 1 to 10 scale figure");
            
            var tags = new List<string>();
            foreach (var tag in tagstring.Split(","))
            {
                var cleanTag = SanitizeTag(tag);
                if (!tags.Contains(cleanTag) && cleanTag.Length < 30)
                    tags.Add(cleanTag);

                if (tags.Count > 12)
                    break;
            }
            return tags;
        }

        public string SanitizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return string.Empty;

            // Entfernt alle Sonderzeichen außer Buchstaben, Zahlen, Leerzeichen und '-'
            tag = Regex.Replace(tag, @"[^a-zA-Z0-9äöüÄÖÜß\s-]", "");

            // Mehrere Leerzeichen zu einem zusammenfassen
            tag = Regex.Replace(tag, @"\s+", " ");

            return tag.Trim();
        }
    }
}
