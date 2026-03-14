// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using G33kSeek.Models;

namespace G33kSeek.Providers;

/// <summary>
/// Provides emoji lookup and emoticon shortcuts for the <c>:</c> prefix.
/// </summary>
/// <remarks>
/// This lets the launcher act as a quick emoji picker whose selected result copies the emoji to the clipboard.
/// </remarks>
public sealed class EmojiQueryProvider : IQueryProvider
{
    private static readonly IReadOnlyList<EmojiDefinition> CommonEmojiEntries =
    [
        new("🙂", "Slightly smiling face", "smile", "happy", ")"),
        new("😊", "Smiling face with smiling eyes", "blush", "pleased"),
        new("😄", "Grinning face with smiling eyes", "grin", "joyful", "d"),
        new("😂", "Face with tears of joy", "joy", "lol", "laugh", "crylaugh"),
        new("😉", "Winking face", "wink"),
        new("😍", "Smiling face with heart-eyes", "heart_eyes", "loveeyes", "adoring"),
        new("🤔", "Thinking face", "thinking", "hmm"),
        new("😎", "Smiling face with sunglasses", "cool", "sunglasses"),
        new("🥳", "Partying face", "party", "celebrate"),
        new("❤️", "Red heart", "heart", "love"),
        new("👍", "Thumbs up", "thumbsup", "+1", "approve", "yes"),
        new("👎", "Thumbs down", "thumbsdown", "-1", "no"),
        new("👋", "Waving hand", "wave", "waving", "hello", "hi"),
        new("👊", "Oncoming fist", "fist_bump", "fistbump", "brofist", "bump"),
        new("🙏", "Folded hands", "pray", "thanks", "please"),
        new("🔥", "Fire", "fire", "lit"),
        new("✨", "Sparkles", "sparkles", "magic"),
        new("🚀", "Rocket", "rocket", "launch"),
        new("💡", "Light bulb", "idea", "lightbulb", "bulb"),
        new("🎉", "Party popper", "tada", "confetti")
    ];

    private static readonly IReadOnlyList<EmojiDefinition> EmojiEntries =
    [
        ..CommonEmojiEntries,
        new("🤝", "Handshake", "handshake", "deal"),
        new("👏", "Clapping hands", "clap", "applause"),
        new("🙌", "Raising hands", "hooray", "celebratehands", "raisehands"),
        new("💪", "Flexed biceps", "muscle", "strong"),
        new("✌️", "Victory hand", "victory", "peace"),
        new("👌", "OK hand", "ok", "okay"),
        new("🤞", "Crossed fingers", "crossedfingers", "fingerscrossed", "luck"),
        new("🤷", "Person shrugging", "shrug", "idk"),
        new("🤦", "Person facepalming", "facepalm", "oops"),
        new("😢", "Crying face", "cry", "tearful"),
        new("😭", "Loudly crying face", "sob", "bawling"),
        new("☹️", "Frowning face", "frown", "sad", "("),
        new("🙁", "Slightly frowning face", "sadface", "unhappy"),
        new("😞", "Disappointed face", "disappointed"),
        new("😟", "Worried face", "worried", "concerned"),
        new("😬", "Grimacing face", "grimace", "awkward"),
        new("😰", "Anxious face with sweat", "nervous", "anxious", "stressed"),
        new("😵", "Face with crossed-out eyes", "dizzy", "overwhelmed"),
        new("😴", "Sleeping face", "sleep", "sleepy"),
        new("🤯", "Exploding head", "mindblown", "mind_blown"),
        new("😮", "Face with open mouth", "surprised", "wow", "gasp"),
        new("😕", "Confused face", "confused"),
        new("😐", "Neutral face", "neutral", "meh"),
        new("😶", "Face without mouth", "speechless", "quiet"),
        new("🤐", "Zipper-mouth face", "zippermouth", "secret"),
        new("😡", "Pouting face", "angry", "mad"),
        new("🤬", "Face with symbols on mouth", "swearing", "rage"),
        new("😅", "Grinning face with sweat", "sweat_smile", "relieved"),
        new("😇", "Smiling face with halo", "innocent", "angel"),
        new("🫡", "Saluting face", "salute", "respect"),
        new("🫠", "Melting face", "melting", "hotmess"),
        new("🤗", "Smiling face with open hands", "hug", "hugging"),
        new("🫶", "Heart hands", "hearthands", "lovehands"),
        new("💔", "Broken heart", "broken_heart", "heartbreak"),
        new("🧠", "Brain", "brain", "smart"),
        new("👀", "Eyes", "eyes", "look"),
        new("✅", "Check mark button", "check", "tick", "done"),
        new("❌", "Cross mark", "cross", "x", "wrong"),
        new("⚠️", "Warning", "warning", "caution"),
        new("⭐", "Star", "star", "favorite"),
        new("🌟", "Glowing star", "glowingstar", "shine"),
        new("🌈", "Rainbow", "rainbow"),
        new("☀️", "Sun", "sun", "sunny"),
        new("🌙", "Crescent moon", "moon", "night"),
        new("☕", "Hot beverage", "coffee", "tea"),
        new("🍺", "Beer mug", "beer", "cheers"),
        new("🎵", "Musical note", "music", "note"),
        new("🎮", "Video game", "gamepad", "gaming"),
        new("💻", "Laptop", "laptop", "computer"),
        new("📱", "Mobile phone", "phone", "mobile"),
        new("🕒", "Three o'clock", "clock", "time"),
        new("⏳", "Hourglass not done", "hourglass", "waiting"),
        new("📌", "Pushpin", "pin", "bookmark"),
        new("📎", "Paperclip", "paperclip", "attach"),
        new("📣", "Megaphone", "megaphone", "announce"),
        new("🧵", "Thread", "thread"),
        new("🪲", "Beetle", "bug", "beetle"),
        new("🛠️", "Hammer and wrench", "tools", "fix", "repair"),
        new("🧪", "Test tube", "test", "science"),
        new("📦", "Package", "package", "parcel"),
        new("📁", "File folder", "folder", "directory"),
        new("🔒", "Locked", "lock", "secure"),
        new("🔓", "Unlocked", "unlock", "openlock"),
        new("➡️", "Right arrow", "right", "arrowright"),
        new("⬅️", "Left arrow", "left", "arrowleft"),
        new("⬆️", "Up arrow", "up", "arrowup"),
        new("⬇️", "Down arrow", "down", "arrowdown")
    ];

    public string Prefix => ":";

    public QueryProviderHelpEntry HelpEntry =>
        new("Emoji", "Use : to copy emojis, for example :smile, :heart, :wave, or :).", ":smile");

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();

        var query = request.ProviderQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(
                new QueryResponse(
                    CommonEmojiEntries.Select(CreateResult).ToArray(),
                    "Emoji mode is ready. Try :smile, :heart, :wave, or :)."));
        }

        var matches = EmojiEntries
            .Select(entry => new EmojiMatch(entry, GetMatchScore(query, entry)))
            .Where(match => match.Score.HasValue)
            .OrderBy(match => match.Score.Value)
            .ThenBy(match => GetPrimaryDisplayAlias(match.Entry).Length)
            .ThenBy(match => match.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(match => CreateResult(match.Entry))
            .Take(25)
            .ToArray();

        if (matches.Length == 0)
        {
            return Task.FromResult(
                new QueryResponse(
                [
                    new QueryResult(
                        "No emoji matched.",
                        $"Nothing matched :{query}. Try :smile, :heart, :wave, :sad, or :).",
                        "Emoji")
                ],
                    "Emoji: no matches found."));
        }

        return Task.FromResult(
            new QueryResponse(
                matches,
                $"Found {matches.Length} emoji match{(matches.Length == 1 ? string.Empty : "es")}. Press Enter to copy the selected emoji."));
    }

    private static QueryResult CreateResult(EmojiDefinition entry)
    {
        var alternateAliases = entry.Aliases
            .Skip(1)
            .Take(3)
            .Select(FormatAlias)
            .ToArray();
        var subtitle = alternateAliases.Length == 0
            ? entry.Name
            : $"{entry.Name}. Also: {string.Join(", ", alternateAliases)}";

        return new QueryResult(
            $"{entry.Symbol}  {FormatAlias(entry.Aliases[0])}",
            subtitle,
            "Emoji",
            new QueryActionDescriptor(
                QueryActionKind.CopyText,
                entry.Symbol,
                successMessage: $"Copied {entry.Symbol} to the clipboard."));
    }

    private static int? GetMatchScore(string query, EmojiDefinition entry)
    {
        var trimmedQuery = query.Trim();
        var rawQuery = trimmedQuery.TrimStart(':');
        var normalizedQuery = Normalize(rawQuery);

        var exactRawAliases = entry.RawAliases.Where(alias => string.Equals(alias, rawQuery, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (exactRawAliases.Length != 0)
            return 0;

        var exactAliases = entry.NormalizedAliases.Where(alias => alias == normalizedQuery).ToArray();
        if (exactAliases.Length != 0)
            return 1;

        if (!string.IsNullOrWhiteSpace(rawQuery) &&
            entry.RawAliases.Any(alias => alias.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase)))
            return 2;

        if (!string.IsNullOrWhiteSpace(normalizedQuery) &&
            entry.NormalizedAliases.Any(alias => alias.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            return 3;

        if (!string.IsNullOrWhiteSpace(normalizedQuery) &&
            entry.NormalizedNameWords.Any(word => word.StartsWith(normalizedQuery, StringComparison.Ordinal)))
            return 4;

        if (!string.IsNullOrWhiteSpace(normalizedQuery) &&
            entry.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
            return 5;

        if (!string.IsNullOrWhiteSpace(normalizedQuery) &&
            entry.NormalizedAliases.Any(alias => alias.Contains(normalizedQuery, StringComparison.Ordinal)))
            return 6;

        return null;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Trim())
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string FormatAlias(string alias) => $":{alias}";

    private static string GetPrimaryDisplayAlias(EmojiDefinition entry) => FormatAlias(entry.Aliases[0]);

    private sealed class EmojiDefinition
    {
        public EmojiDefinition(string symbol, string name, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(symbol));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            if (aliases == null || aliases.Length == 0 || aliases.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("At least one alias is required.", nameof(aliases));

            Symbol = symbol;
            Name = name;
            Aliases = aliases;
            RawAliases = aliases.Select(alias => alias.Trim()).ToArray();
            NormalizedAliases = RawAliases.Select(Normalize).ToArray();
            NormalizedName = Normalize(name);
            NormalizedNameWords = name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Normalize)
                .Where(word => word.Length > 0)
                .ToArray();
        }

        public string Symbol { get; }

        public string Name { get; }

        public IReadOnlyList<string> Aliases { get; }

        public IReadOnlyList<string> RawAliases { get; }

        public IReadOnlyList<string> NormalizedAliases { get; }

        public string NormalizedName { get; }

        public IReadOnlyList<string> NormalizedNameWords { get; }
    }

    private sealed class EmojiMatch
    {
        public EmojiMatch(EmojiDefinition entry, int? score)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Score = score;
        }

        public EmojiDefinition Entry { get; }

        public int? Score { get; }
    }
}
