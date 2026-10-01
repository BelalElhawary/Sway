using System.Net;

namespace Sway.Core.Dom;

/// <summary>
/// Minimal HTML fragment parser. The Razor compiler emits static subtrees as markup frames,
/// so the renderer needs to turn those strings into nodes.
/// </summary>
public static class HtmlFragment
{
    static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"
    };

    public static void ParseInto(ContainerNode target, string html)
    {
        var doc = target.Document;
        var stack = new Stack<ContainerNode>();
        stack.Push(target);
        int i = 0;

        while (i < html.Length)
        {
            if (html[i] != '<')
            {
                int next = html.IndexOf('<', i);
                if (next < 0) next = html.Length;
                AddText(stack.Peek(), doc, html.Substring(i, next - i));
                i = next;
                continue;
            }

            if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
            {
                int end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? html.Length : end + 3;
                continue;
            }

            if (i + 1 < html.Length && html[i + 1] == '/')
            {
                int end = html.IndexOf('>', i);
                if (end < 0) break;
                string name = html.Substring(i + 2, end - i - 2).Trim();
                PopTo(stack, name);
                i = end + 1;
                continue;
            }

            i = ParseOpenTag(html, i, stack, doc);
        }
    }

    static void AddText(ContainerNode parent, Document doc, string raw)
    {
        if (raw.Length == 0) return;
        parent.Append(new TextNode(doc, WebUtility.HtmlDecode(raw)));
    }

    static void PopTo(Stack<ContainerNode> stack, string tag)
    {
        // Only unwind if a matching element is open; stray end tags are ignored.
        if (!stack.Any(n => n is ElementNode e && e.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))) return;
        while (stack.Count > 1)
        {
            var popped = stack.Pop();
            if (popped is ElementNode e && e.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)) return;
        }
    }

    static int ParseOpenTag(string s, int start, Stack<ContainerNode> stack, Document doc)
    {
        int i = start + 1;
        int nameStart = i;
        while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '>' && s[i] != '/') i++;
        var element = new ElementNode(doc, s.Substring(nameStart, i - nameStart));
        bool selfClosing = false;

        while (i < s.Length)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) break;
            if (s[i] == '>') { i++; break; }
            if (s[i] == '/') { selfClosing = true; i++; continue; }

            int attrStart = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '=' && s[i] != '>' && s[i] != '/') i++;
            string attr = s.Substring(attrStart, i - attrStart);
            string value = "";

            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i < s.Length && s[i] == '=')
            {
                i++;
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i < s.Length && (s[i] == '"' || s[i] == '\''))
                {
                    char quote = s[i++];
                    int end = s.IndexOf(quote, i);
                    if (end < 0) end = s.Length;
                    value = s.Substring(i, end - i);
                    i = Math.Min(end + 1, s.Length);
                }
                else
                {
                    int valueStart = i;
                    while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '>') i++;
                    value = s.Substring(valueStart, i - valueStart);
                }
            }
            if (attr.Length > 0) element.SetAttribute(attr, WebUtility.HtmlDecode(value));
        }

        stack.Peek().Append(element);
        if (!selfClosing && !VoidTags.Contains(element.Tag)) stack.Push(element);
        return i;
    }
}
