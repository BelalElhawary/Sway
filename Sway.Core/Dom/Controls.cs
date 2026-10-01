namespace Sway.Core.Dom;

/// <summary>Classification of the built-in form controls, which draw themselves instead of laying out children.</summary>
public static class Controls
{
    static readonly HashSet<string> NonTextTypes = new()
    {
        "checkbox", "radio", "button", "submit", "reset", "hidden", "range", "color", "file", "image"
    };

    /// <summary>The effective input type, defaulting to text.</summary>
    public static string InputType(ElementNode el) =>
        (el.GetAttribute("type") ?? "text").Trim().ToLowerInvariant();

    public static bool IsInput(ElementNode el) => el.Tag == "input";

    /// <summary>Any input type not handled by another control is edited as text.</summary>
    public static bool IsTextInput(ElementNode el) =>
        el.Tag == "input" && !NonTextTypes.Contains(InputType(el));

    public static bool IsTextArea(ElementNode el) => el.Tag == "textarea";

    /// <summary>input (text-like) or textarea: something the user types into.</summary>
    public static bool IsTextEditable(ElementNode el) => IsTextInput(el) || IsTextArea(el);

    public static bool IsCheckable(ElementNode el) =>
        el.Tag == "input" && InputType(el) is "checkbox" or "radio";

    public static bool IsRadio(ElementNode el) => el.Tag == "input" && InputType(el) == "radio";

    /// <summary>input type=button/submit/reset: drawn as a button labelled by its value.</summary>
    public static bool IsButtonInput(ElementNode el) =>
        el.Tag == "input" && InputType(el) is "button" or "submit" or "reset";

    public static bool IsSelect(ElementNode el) => el.Tag == "select";

    /// <summary>Controls whose contents are drawn by the toolkit rather than laid out from child nodes.</summary>
    public static bool IsReplaced(ElementNode el) =>
        el.Tag is "input" or "textarea" or "select";

    public static bool IsPassword(ElementNode el) => el.Tag == "input" && InputType(el) == "password";

    public static bool IsReadOnly(ElementNode el) => el.Attributes.ContainsKey("readonly");

    public static int IntAttribute(ElementNode el, string name, int fallback) =>
        el.GetAttribute(name) is { } s && int.TryParse(s, out var n) && n > 0 ? n : fallback;

    // ---- select ----

    public sealed record SelectOption(ElementNode Element, string Value, string Label, bool Disabled);

    public static List<SelectOption> Options(ElementNode select)
    {
        var result = new List<SelectOption>();
        void Walk(ElementNode parent)
        {
            foreach (var child in parent.PhysicalChildren)
            {
                if (child is not ElementNode e) continue;
                if (e.Tag == "option")
                {
                    string label = TextContent(e).Trim();
                    result.Add(new SelectOption(e, e.GetAttribute("value") ?? label, label, e.IsDisabled));
                }
                else if (e.Tag == "optgroup") Walk(e);
            }
        }
        Walk(select);
        return result;
    }

    /// <summary>Index of the selected option: the select's value, else an option marked selected, else the first.</summary>
    public static int SelectedIndex(ElementNode select, List<SelectOption> options)
    {
        if (options.Count == 0) return -1;
        if (select.GetAttribute("value") is { } value)
        {
            int match = options.FindIndex(o => o.Value == value);
            if (match >= 0) return match;
        }
        int marked = options.FindIndex(o => o.Element.Attributes.ContainsKey("selected"));
        return marked >= 0 ? marked : 0;
    }

    public static string TextContent(ElementNode e)
    {
        var sb = new System.Text.StringBuilder();
        void Walk(ElementNode node)
        {
            foreach (var child in node.PhysicalChildren)
            {
                if (child is TextNode t) sb.Append(t.Text);
                else if (child is ElementNode el) Walk(el);
            }
        }
        Walk(e);
        return sb.ToString();
    }
}
