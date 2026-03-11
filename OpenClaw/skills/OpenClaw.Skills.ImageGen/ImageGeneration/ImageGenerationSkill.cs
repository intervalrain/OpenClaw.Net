using System.ComponentModel;

using OpenAI;
using OpenAI.Images;

using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.ImageGen.ImageGeneration;

public class ImageGenerationSkill : AgentSkillBase<ImageGenerationSkillArgs>
{
    public override string Name => "image_generation";
    public override string Description => """
        Generate images using OpenAI DALL-E. Use when: creating images from text descriptions,
        generating artwork, logos, illustrations, or any visual content.
        Requires OPENAI_API_KEY environment variable.
        """;

    public override async Task<SkillResult> ExecuteAsync(ImageGenerationSkillArgs args, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            return SkillResult.Failure("OPENAI_API_KEY environment variable is not set.");

        if (string.IsNullOrWhiteSpace(args.Prompt))
            return SkillResult.Failure("Prompt is required for image generation.");

        try
        {
            var client = new OpenAIClient(apiKey);
            var imageClient = client.GetImageClient(args.Model ?? "dall-e-3");

            var options = new ImageGenerationOptions
            {
                Quality = args.Quality?.ToLowerInvariant() switch
                {
                    "hd" or "high" => GeneratedImageQuality.High,
                    _ => GeneratedImageQuality.Standard
                },
                Size = ParseSize(args.Size),
                Style = args.Style?.ToLowerInvariant() switch
                {
                    "natural" => GeneratedImageStyle.Natural,
                    _ => GeneratedImageStyle.Vivid
                },
                ResponseFormat = GeneratedImageFormat.Uri
            };

            var response = await imageClient.GenerateImageAsync(args.Prompt, options, ct);

            if (response?.Value == null)
                return SkillResult.Failure("No image was generated.");

            var result = response.Value;
            var output = $"""
                Image generated successfully!

                URL: {result.ImageUri}

                Revised Prompt: {result.RevisedPrompt ?? "(none)"}

                Note: This URL is temporary and will expire. Download the image if you need to keep it.
                """;

            return SkillResult.Success(output);
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Image generation failed: {ex.Message}");
        }
    }

    private static GeneratedImageSize ParseSize(string? size)
    {
        return size?.ToLowerInvariant() switch
        {
            "1024x1024" or "square" => GeneratedImageSize.W1024xH1024,
            "1792x1024" or "landscape" or "wide" => GeneratedImageSize.W1792xH1024,
            "1024x1792" or "portrait" or "tall" => GeneratedImageSize.W1024xH1792,
            _ => GeneratedImageSize.W1024xH1024
        };
    }
}

public record ImageGenerationSkillArgs(
    [property: Description("""
        The text description of the image to generate.
        Be descriptive and specific for better results.
        Example: 'A serene mountain landscape at sunset with snow-capped peaks reflected in a crystal-clear lake'
        """)]
    string? Prompt,

    [property: Description("""
        Image size. Options:
        - '1024x1024' or 'square' (default)
        - '1792x1024' or 'landscape' or 'wide'
        - '1024x1792' or 'portrait' or 'tall'
        """)]
    string? Size = "1024x1024",

    [property: Description("""
        Image quality. Options:
        - 'standard' (default, faster)
        - 'hd' or 'high' (more detailed, slower)
        """)]
    string? Quality = "standard",

    [property: Description("""
        Image style. Options:
        - 'vivid' (default, hyper-real and dramatic)
        - 'natural' (more natural, less hyper-real)
        """)]
    string? Style = "vivid",

    [property: Description("Model to use: 'dall-e-3' (default, best quality) or 'dall-e-2' (faster, cheaper)")]
    string? Model = "dall-e-3"
);
