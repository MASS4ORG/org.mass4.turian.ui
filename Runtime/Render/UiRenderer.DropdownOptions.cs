using System.Runtime.CompilerServices;

namespace Turian.Engine.UI;

public sealed partial class UiRenderer
{
    const string optionTag = "Option";

    /// <summary>Style source of options that come from bound items or an enum, which have no element of their own.</summary>
    static readonly UiElement itemOption = new() { Tag = optionTag };

    /// <summary>Options per enum type, without keeping collectible user assemblies alive.</summary>
    static readonly ConditionalWeakTable<Type, DropdownOption[]> enumOptions = [];

    /// <summary>One choice of a dropdown: its label, the value a binding receives, and the element that styles it.</summary>
    sealed record DropdownOption(string Label, object? Value, UiElement Style);

    List<DropdownOption> DropdownOptions(UiElement el)
    {
        var (bound, items) = BoundValue(el, "items");
        return bound ? ItemOptions(items, Attr(el, "display-member")) : ChildOptions(el);
    }

    static List<DropdownOption> ItemOptions(object? items, string? displayMember)
    {
        var options = new List<DropdownOption>();
        if (items is not IEnumerable list || items is string)
        {
            return options;
        }

        foreach (var item in list)
        {
            var label = displayMember is null ? item : MemberPath.Read(item, displayMember);
            options.Add(new DropdownOption(label?.ToString() ?? string.Empty, item, itemOption));
        }

        return options;
    }

    List<DropdownOption> ChildOptions(UiElement el)
    {
        var options = new List<DropdownOption>();
        foreach (var child in el.Children)
        {
            if (child.Tag == optionTag)
            {
                var label = ResolveText(child) ?? string.Empty;
                options.Add(new DropdownOption(label, Attr(child, "value") ?? label, child));
            }
        }

        return options;
    }

    IReadOnlyList<DropdownOption> EnumOptions(UiElement el) =>
        BoundValue(el, "value").Value is Enum current
            ? enumOptions.GetValue(current.GetType(), static type => [.. Enum.GetValues(type).Cast<Enum>().Distinct()
                .Select(static value => new DropdownOption(value.ToString(), value, itemOption))])
            : [];

    /// <summary>
    /// The chosen option: a bound <c>selected-index</c>, else the option equal to a bound <c>value</c>, else the last
    /// choice, else the literal <c>selected-index</c> or <c>value</c>; -1 when none matches.
    /// </summary>
    int SelectedIndex(UiElement el, IReadOnlyList<DropdownOption> options, string key)
    {
        var (indexBound, index) = BoundValue(el, "selected-index");
        if (indexBound)
        {
            return ToNumber(index) is { } i ? (int)i : -1;
        }

        var (valueBound, value) = BoundValue(el, "value");
        if (valueBound)
        {
            return IndexOfValue(options, value);
        }

        if (state.TryGetValue(key, out var cached) && cached is int stored)
        {
            return stored;
        }

        return UiValue.TryFloat(Attr(el, "selected-index"), out var literal)
            ? (int)literal
            : IndexOfValue(options, Attr(el, "value"));
    }

    static int IndexOfValue(IReadOnlyList<DropdownOption> options, object? value)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (Equals(options[i].Value, value) || (value is string text && options[i].Value?.ToString() == text))
            {
                return i;
            }
        }

        return -1;
    }
}
