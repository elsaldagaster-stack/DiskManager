using DiskManager.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DiskManager.Controls;

public class TreeMapPanel : Panel
{
    public static readonly DependencyProperty RootNodeProperty =
        DependencyProperty.Register(nameof(RootNode), typeof(FolderNode), typeof(TreeMapPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty NodeClickCommandProperty =
        DependencyProperty.Register(nameof(NodeClickCommand), typeof(ICommand), typeof(TreeMapPanel));

    public FolderNode? RootNode
    {
        get => (FolderNode?)GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public ICommand? NodeClickCommand
    {
        get => (ICommand?)GetValue(NodeClickCommandProperty);
        set => SetValue(NodeClickCommandProperty, value);
    }

    private static readonly Brush[] _palette =
    {
        new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)),
        new SolidColorBrush(Color.FromRgb(0xCB, 0xA6, 0xF7)),
        new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
        new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)),
        new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF)),
        new SolidColorBrush(Color.FromRgb(0x89, 0xDC, 0xEB)),
    };

    private readonly List<(FolderNode Node, Rect Rect, Brush Fill)> _rects = new();

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override Size ArrangeOverride(Size finalSize)
    {
        _rects.Clear();
        InternalChildren.Clear();

        if (RootNode is null || RootNode.TotalSize == 0) return finalSize;

        var nodes = RootNode.Children
            .Where(c => c.TotalSize > 0)
            .OrderByDescending(c => c.TotalSize)
            .ToList();

        Squarify(nodes, new Rect(0, 0, finalSize.Width, finalSize.Height), RootNode.TotalSize);
        return finalSize;
    }

    private void Squarify(List<FolderNode> nodes, Rect area, long totalSize)
    {
        if (nodes.Count == 0 || area.Width < 2 || area.Height < 2) return;

        double remaining = area.Height;
        double y = area.Y;
        int colorIdx = 0;

        foreach (var node in nodes)
        {
            double ratio = totalSize > 0 ? (double)node.TotalSize / totalSize : 0;
            double height = remaining * ratio;
            if (height < 1) height = 1;

            var rect = new Rect(area.X, y, area.Width, height);
            var fill = _palette[colorIdx % _palette.Length];
            _rects.Add((node, rect, fill));
            colorIdx++;
            y += height;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var textBrush = new SolidColorBrush(Colors.Black);
        foreach (var (node, rect, fill) in _rects)
        {
            dc.DrawRectangle(fill, new Pen(Brushes.White, 1), rect);
            if (rect.Width > 40 && rect.Height > 20)
            {
                var ft = new FormattedText(
                    $"{node.Name}\n{FormatSize(node.TotalSize)}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10, textBrush, 96);
                dc.DrawText(ft, new Point(rect.X + 4, rect.Y + 4));
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);
        var hit = _rects.FirstOrDefault(r => r.Rect.Contains(pos));
        if (hit.Node is not null && NodeClickCommand?.CanExecute(hit.Node) == true)
            NodeClickCommand.Execute(hit.Node);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
