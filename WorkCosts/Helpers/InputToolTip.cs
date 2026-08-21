using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace WorkCosts.Helpers;

public static class InputToolTip
{
    public static string Format(string prompt, string? value) =>
        $"{prompt}: {value?.Trim() ?? string.Empty}";

    public static void Set(DependencyObject target, string prompt, string? value) =>
        ToolTipService.SetToolTip(target, Format(prompt, value));

    public static void Bind(TextBox box)
    {
        var prompt = PromptOf(box.Header, box.PlaceholderText);
        void Update() => Set(box, prompt, box.Text);
        box.TextChanged += (_, _) => Update();
        Update();
    }

    public static void Bind(NumberBox box)
    {
        var prompt = PromptOf(box.Header, box.PlaceholderText);
        void Update()
        {
            var text = double.IsNaN(box.Value)
                ? string.Empty
                : box.Value.ToString("0.##", CultureInfo.CurrentCulture);
            Set(box, prompt, text);
        }

        box.ValueChanged += (_, _) => Update();
        Update();
    }

    public static void Bind(ComboBox box, Func<string?> getValue)
    {
        var prompt = PromptOf(box.Header, box.PlaceholderText);
        void Update() => Set(box, prompt, getValue());
        box.SelectionChanged += (_, _) => Update();
        Update();
    }

    public static void Bind(RadioButtons radios, Func<string?> getValue)
    {
        var prompt = PromptOf(radios.Header, null);
        void Update() => Set(radios, prompt, getValue());
        radios.SelectionChanged += (_, _) => Update();
        Update();
    }

    public static void Bind(ToggleSwitch toggle, string prompt)
    {
        void Update()
        {
            var value = toggle.IsOn
                ? toggle.OnContent?.ToString()
                : toggle.OffContent?.ToString();
            Set(toggle, prompt, value);
        }

        toggle.Toggled += (_, _) => Update();
        Update();
    }

    public static string PromptOf(object? header, string? placeholder)
    {
        if (header is string headerText && !string.IsNullOrWhiteSpace(headerText))
        {
            return headerText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            return placeholder.Trim();
        }

        return "Value";
    }
}

public sealed class PromptValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var prompt = parameter as string ?? "Value";
        return InputToolTip.Format(prompt, value?.ToString());
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
