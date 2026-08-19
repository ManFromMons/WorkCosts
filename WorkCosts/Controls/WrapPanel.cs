using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace WorkCosts.Controls;

/// <summary>Flex-style panel: children flow left-to-right and wrap, each sized to its content.</summary>
public sealed class WrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(
            nameof(HorizontalSpacing),
            typeof(double),
            typeof(WrapPanel),
            new PropertyMetadata(10d, OnSpacingChanged));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(
            nameof(VerticalSpacing),
            typeof(double),
            typeof(WrapPanel),
            new PropertyMetadata(10d, OnSpacingChanged));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((WrapPanel)d).InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : availableSize.Width;

        var x = 0d;
        var y = 0d;
        var rowHeight = 0d;
        var panelWidth = 0d;
        var panelHeight = 0d;

        foreach (var child in Children)
        {
            if (child is not UIElement element)
                continue;

            element.Measure(new Size(maxWidth, availableSize.Height));
            var desired = element.DesiredSize;

            if (x > 0 && x + desired.Width > maxWidth)
            {
                panelWidth = Math.Max(panelWidth, x - HorizontalSpacing);
                y += rowHeight + VerticalSpacing;
                x = 0;
                rowHeight = 0;
            }

            x += desired.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, desired.Height);
            panelHeight = y + rowHeight;
        }

        panelWidth = Math.Max(panelWidth, Math.Max(0, x - HorizontalSpacing));
        return new Size(panelWidth, panelHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var maxWidth = finalSize.Width;
        var x = 0d;
        var y = 0d;
        var rowHeight = 0d;

        foreach (var child in Children)
        {
            if (child is not UIElement element)
                continue;

            var desired = element.DesiredSize;
            if (x > 0 && x + desired.Width > maxWidth)
            {
                y += rowHeight + VerticalSpacing;
                x = 0;
                rowHeight = 0;
            }

            element.Arrange(new Rect(x, y, desired.Width, desired.Height));
            x += desired.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, desired.Height);
        }

        return finalSize;
    }
}
