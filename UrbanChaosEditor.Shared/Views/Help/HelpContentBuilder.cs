using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace UrbanChaosEditor.Shared.Views.Help;

public sealed class HelpSection
{
    public HelpSection(string title, params string[] paragraphs)
    {
        Title = title;
        Paragraphs = paragraphs;
    }

    public string Title { get; }
    public IReadOnlyList<string> Paragraphs { get; }
}

public static class HelpContentBuilder
{
    private static readonly Brush Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22));
    private static readonly Brush Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
    private static readonly Brush AccentGold = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly Brush AccentBlue = new SolidColorBrush(Color.FromRgb(0x7A, 0xD7, 0xFF));

    public static FlowDocument Create(string title, string intro, params HelpSection[] sections)
    {
        var doc = new FlowDocument
        {
            Background = Background,
            Foreground = Foreground,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            PagePadding = new Thickness(32, 24, 32, 24),
            ColumnWidth = 100000
        };

        doc.Blocks.Add(new Paragraph(new Run(title))
        {
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = AccentGold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        doc.Blocks.Add(new Paragraph(new Run(intro))
        {
            FontSize = 15,
            Foreground = Muted,
            Margin = new Thickness(0, 0, 0, 20)
        });

        foreach (var section in sections)
        {
            doc.Blocks.Add(new Paragraph(new Run(section.Title))
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = AccentBlue,
                Margin = new Thickness(0, 16, 0, 6)
            });

            foreach (var paragraph in section.Paragraphs)
            {
                AddParagraphOrList(doc, paragraph);
            }
        }

        return doc;
    }

    private static void AddParagraphOrList(FlowDocument doc, string text)
    {
        if (!text.StartsWith("- "))
        {
            doc.Blocks.Add(new Paragraph(new Run(text))
            {
                Margin = new Thickness(0, 0, 0, 8),
                LineHeight = 22
            });
            return;
        }

        var list = new List
        {
            MarkerStyle = TextMarkerStyle.Disc,
            Margin = new Thickness(20, 0, 0, 10),
            Padding = new Thickness(0)
        };

        foreach (var item in text.Split('\n'))
        {
            var clean = item.StartsWith("- ") ? item[2..] : item;
            list.ListItems.Add(new ListItem(new Paragraph(new Run(clean))
            {
                Margin = new Thickness(0, 0, 0, 4)
            }));
        }

        doc.Blocks.Add(list);
    }
}
