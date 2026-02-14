using System.Text.RegularExpressions;

using Weda.Core.Infrastructure.Messaging.Nats.Abstractions;

namespace Weda.Protocol;

public partial class WedaSubjectResolver : ISubjectResolver
{
    // eco + version(1 digit) + format(j/p)
    [GeneratedRegex(@"^eco(\d)([jp])")]
    private static partial Regex PrefixRegex();

    public bool CanResolve(string subject)
    {
        if (string.IsNullOrEmpty(subject))
            return false;
        
        var firstDot = subject.IndexOf('.');
        var prefix = firstDot > 0 ? subject[..firstDot] : subject;

        return PrefixRegex().IsMatch(prefix);
    }

    public SubjectInfo Parse(string subject)
    {
        var parts = subject.Split('.');

        if (parts.Length < 6)
            throw new ArgumentException("$Invalid ECO subject format: {subject}");

        var prefix = parts[0];
        var match = PrefixRegex().Match(prefix);

        if (!match.Success)
            throw new ArgumentException($"Invalid ECO prefix: {prefix}");

        var version = int.Parse(match.Groups[1].Value);
        var protocol = match.Groups[2].Value switch
        {
            "j" => "application/json",
            "p" => "application/protobuf",
            _ => throw new ArgumentException($"Invalid ECO prefix: {prefix}")
        };

        return new SubjectInfo
        {
            Protocol = prefix,
            Version = version,
            ContentType = protocol,
            Segments = new Dictionary<string, string>
            {
                ["groupId"] = parts[1],
                ["resourceId"] = parts[2],
                ["from"] = parts[3],
                ["to"] = parts[4],
                ["messageType"] = parts[5],
                ["params"] = parts.Length > 6 ? string.Join(".", parts[6..]) : string.Empty
            }
        };
    }

    public string Build(SubjectInfo info)
    {
        var segments = info.Segments;

        var baseParts = new[]
        {
            info.Protocol,
            segments.GetValueOrDefault("groupId", ""),
            segments.GetValueOrDefault("resourceId", ""),
            segments.GetValueOrDefault("from", ""),
            segments.GetValueOrDefault("to", ""),
            segments.GetValueOrDefault("messageType", "")
        };

        var result = string.Join(".", baseParts);

        if (segments.TryGetValue("params", out var p) && !string.IsNullOrEmpty(p))
        {
            result = $"{result}.{p}";
        }

        return result;
    }
}