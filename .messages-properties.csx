#r "nuget: Lestaly, 0.58.0"
#nullable enable
using System.Diagnostics.CodeAnalysis;
using Lestaly;

enum MessageEntryType
{
    Resource,
    Plain,
}

record MessageEntry(MessageEntryType Type, MessageText? Resource = default, string? Line = default)
{
    public static MessageEntry OfResource(MessageText resource) => new(MessageEntryType.Resource, Resource: resource);
    public static MessageEntry OfPlain(string line) => new(MessageEntryType.Plain, Line: line);

    [property: MemberNotNullWhen(true, nameof(Resource))]
    public bool IsResource => this.Type == MessageEntryType.Resource;
    public bool IsPlain => this.Type == MessageEntryType.Plain;
}

record MessageText(string Key, string Text);

IEnumerable<MessageEntry> LoadMessageEntries(FileInfo file, string? lineEnd = default)
{
    var term = lineEnd ?? Environment.NewLine;
    var multiline = default(string);
    var linebuffer = new StringBuilder();

    MessageEntry? decisionMultiline()
    {
        if (multiline == null) return default;

        var entry = MessageEntry.OfResource(new(multiline, linebuffer!.ToString()));
        multiline = null;
        linebuffer.Clear();

        return entry;
    }

    foreach (var line in file.ReadAllLines())
    {
        var body = line.AsSpan().TrimStart();
        if (body.IsEmpty)
        {
            // Blank line
            if (decisionMultiline() is MessageEntry entry) yield return entry;
            yield return MessageEntry.OfPlain(line);
        }
        else if (body.StartsWith("#"))
        {
            // Comment line
            if (decisionMultiline() is MessageEntry entry) yield return entry;
            yield return MessageEntry.OfPlain(line);
        }
        else if (multiline != null)
        {
            // continued from previous line
            if (body.EndsWith(@"\"))
            {
                // continue to next line
                linebuffer.Append($"{body}{term}");
            }
            else if (decisionMultiline() is MessageEntry entry)
            {
                yield return entry;
            }
        }
        else if (body.IndexOf('=') is var sepaPos && 0 <= sepaPos)
        {
            // Key=value format
            var key = body[..sepaPos].ToString();
            var value = body[(sepaPos + 1)..].ToString();
            if (value.EndsWith(@"\"))
            {
                // continue to next line
                multiline = key;
                linebuffer.Clear();
                linebuffer.Append($"{value}{term}");
            }
            else
            {
                yield return MessageEntry.OfResource(new(key, value));
            }
        }
        else
        {
            // unknown 
            if (decisionMultiline() is MessageEntry entry) yield return entry;
            yield return MessageEntry.OfPlain(line);
        }
    }
}

void SaveMessageEntries(FileInfo file, IEnumerable<MessageEntry> entries, string? lineEnd = default, Encoding? encoding = default)
{
    using var writer = file.CreateTextWriter(encoding: encoding);
    writer.NewLine = lineEnd;
    foreach (var entry in entries)
    {
        if (entry.IsResource)
        {
            writer.WriteLine($"{entry.Resource.Key}={entry.Resource.Text}");
        }
        else
        {
            writer.WriteLine($"{entry.Line}");
        }
    }

}