// <summary>
// Extended ecosystem model providers inspired by opencode-dev provider specifications.
// Provides out-of-the-box configurations for specialized and global AI providers.
// </summary>

namespace OpenClawTray.Presentation;

/// <summary>Represents a pre-configured ecosystem provider template.</summary>
internal sealed record ExtendedProviderPreset(
    string Id,
    string DisplayName,
    string DefaultBaseUrl,
    string ApiType,
    string DefaultModels,
    string Description);

/// <summary>Catalog of specialized ecosystem model providers.</summary>
internal static class ExtendedModelProviderCatalog
{
    public static readonly IReadOnlyList<ExtendedProviderPreset> Presets =
    [
        new("alibaba", "Alibaba Cloud (DashScope / Qwen)", "https://dashscope-intl.aliyuncs.com/compatible-mode/v1", ModelProviderWire.OpenAiCompletionsApi, "qwen-plus, qwen-max, qwen-turbo", "Alibaba Cloud DashScope with native Qwen model routing"),
        new("amazon-bedrock", "Amazon Bedrock", "https://bedrock-runtime.us-east-1.amazonaws.com", ModelProviderWire.OpenAiCompletionsApi, "anthropic.claude-3-5-sonnet-20241022-v2:0", "AWS Amazon Bedrock foundation models"),
        new("azure", "Azure OpenAI", "https://{resource}.openai.azure.com/openai/deployments/{deployment}", ModelProviderWire.OpenAiCompletionsApi, "gpt-4o, gpt-4o-mini", "Microsoft Azure enterprise OpenAI deployments"),
        new("baseten", "Baseten", "https://inference.baseten.co/v1", ModelProviderWire.OpenAiCompletionsApi, "meta-llama/Llama-3.3-70B-Instruct", "Dedicated low-latency model inference on Baseten"),
        new("byteplus", "BytePlus", "https://ark.cn-beijing.volces.com/api/v3", ModelProviderWire.OpenAiCompletionsApi, "doubao-pro-32k", "ByteDance BytePlus international AI foundation models"),
        new("cerebras", "Cerebras", "https://api.cerebras.ai/v1", ModelProviderWire.OpenAiCompletionsApi, "llama3.3-70b, llama3.1-8b", "Wafer-scale high-speed inference for Llama models"),
        new("cloudflare-ai-gateway", "Cloudflare AI Gateway", "https://gateway.ai.cloudflare.com/v1/{account_id}/{gateway_id}/openai", ModelProviderWire.OpenAiCompletionsApi, "@cf/meta/llama-3.3-70b-instruct", "Unified AI analytics, caching and rate limiting on Cloudflare"),
        new("cloudflare-workers-ai", "Cloudflare Workers AI", "https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1", ModelProviderWire.OpenAiCompletionsApi, "@cf/meta/llama-3.3-70b-instruct", "Serverless GPU inference at Cloudflare global edge"),
        new("cohere", "Cohere", "https://api.cohere.ai/compatibility/v1", ModelProviderWire.OpenAiCompletionsApi, "command-r-plus-08-2024, command-r-08-2024", "Enterprise multilingual generation and RAG reasoning"),
        new("deepinfra", "DeepInfra", "https://api.deepinfra.com/v1/openai", ModelProviderWire.OpenAiCompletionsApi, "meta-llama/Llama-3.3-70B-Instruct, deepseek-ai/DeepSeek-V3", "Cost-effective serverless inference for open-weight LLMs"),
        new("deepseek", "DeepSeek", "https://api.deepseek.com/v1", ModelProviderWire.OpenAiCompletionsApi, "deepseek-chat, deepseek-reasoner", "DeepSeek V3 and DeepSeek R1 reasoning models"),
        new("digitalocean", "DigitalOcean GenAI", "https://inference.do-ai.run/v1", ModelProviderWire.OpenAiCompletionsApi, "llama-3.3-70b", "DigitalOcean GenAI Platform hosted models"),
        new("elevenlabs", "ElevenLabs", "https://api.elevenlabs.io/v1", ModelProviderWire.OpenAiCompletionsApi, "eleven_multilingual_v2", "Generative voice and audio models"),
        new("fireworks", "Fireworks AI", "https://api.fireworks.ai/inference/v1", ModelProviderWire.OpenAiCompletionsApi, "accounts/fireworks/models/llama-v3p3-70b-instruct, accounts/fireworks/models/deepseek-v3", "Fast production inference for compound AI systems"),
        new("groq", "Groq", "https://api.groq.com/openai/v1", ModelProviderWire.OpenAiCompletionsApi, "llama-3.3-70b-versatile, llama-3.1-8b-instant, mixtral-8x7b-32768", "Ultra-fast LPU inference engine for open models"),
        new("mistral", "Mistral AI", "https://api.mistral.ai/v1", ModelProviderWire.OpenAiCompletionsApi, "mistral-large-latest, mistral-small-latest, codestral-latest", "Frontier European models: Mistral Large, Small, and Codestral"),
        new("moonshot", "Moonshot AI (Kimi)", "https://api.moonshot.ai/v1", ModelProviderWire.OpenAiCompletionsApi, "moonshot-v1-8k, moonshot-v1-32k, moonshot-v1-128k", "High-capacity context window models from Moonshot"),
        new("venice", "Venice AI", "https://api.venice.ai/api/v1", ModelProviderWire.OpenAiCompletionsApi, "llama-3.3-70b, deepseek-r1-671b", "Private, uncensored and zero-retention AI inference"),
        new("vercel-ai-gateway", "Vercel AI Gateway", "https://ai-gateway.vercel.sh/v1", ModelProviderWire.OpenAiCompletionsApi, "anthropic/claude-3-5-sonnet, openai/gpt-4o", "Edge-optimized multi-provider routing on Vercel"),
        new("volcengine", "Volcengine (Doubao)", "https://ark.cn-beijing.volces.com/api/v3", ModelProviderWire.OpenAiCompletionsApi, "doubao-pro-32k, doubao-lite-32k", "ByteDance Volcano Engine enterprise models"),
        new("zai", "ZAI (Zhipu GLM)", "https://api.z.ai/api/paas/v4", ModelProviderWire.OpenAiCompletionsApi, "glm-4-plus, glm-4-air", "Zhipu AI GLM-4 flagship generation and reasoning models")
    ];

    public static ExtendedProviderPreset? Find(string id) =>
        Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
