using OpenAI.Chat;

namespace EtsyBacklogListingGenerator.AI
{
    public class OpenAIManager
    {
        private const string DefaultModel = "gpt-5.1";
        private const string ApiKeyEnvironmentVariable = "OPENAI_API_KEY";
        private const string DefaultSystemPromptPath = "Prompts/system-prompt.md";

        private readonly ChatClient _client;
        private readonly string _systemPromptPath;

        public OpenAIManager(
            string? systemPromptPath = null,
            string? model = null,
            string? apiKey = null)
        {
            apiKey ??= Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    $"OpenAI API key is missing. Set the {ApiKeyEnvironmentVariable} environment variable.");
            }

            _client = new ChatClient(model ?? DefaultModel, apiKey);
            _systemPromptPath = ResolvePromptPath(systemPromptPath ?? DefaultSystemPromptPath);
        }

        public async Task<string> AskAsync(string input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input must not be empty.", nameof(input));
            }

            var systemPrompt = await File.ReadAllTextAsync(_systemPromptPath, cancellationToken);

            ChatMessage[] messages =
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(input)
            ];

            ChatCompletion completion = await _client.CompleteChatAsync(
                messages,
                cancellationToken: cancellationToken);

            return string.Join(
                Environment.NewLine,
                completion.Content
                    .Where(contentPart => !string.IsNullOrWhiteSpace(contentPart.Text))
                    .Select(contentPart => contentPart.Text));
        }

        private static string ResolvePromptPath(string systemPromptPath)
        {
            if (Path.IsPathRooted(systemPromptPath))
            {
                return systemPromptPath;
            }

            return Path.Combine(AppContext.BaseDirectory, systemPromptPath);
        }
    }
}
