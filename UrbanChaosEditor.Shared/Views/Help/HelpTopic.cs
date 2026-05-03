using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;

namespace UrbanChaosEditor.Shared.Views.Help;

public sealed class HelpTopic
{
    public HelpTopic(string fileName, string displayName, Func<FlowDocument> createDocument)
    {
        FileName = fileName;
        DisplayName = displayName;
        CreateDocument = createDocument;
    }

    public string FileName { get; }
    public string DisplayName { get; }
    public Func<FlowDocument> CreateDocument { get; }

    public static HelpTopic FromResource(string fileName, string displayName, string assemblyName)
    {
        return new HelpTopic(
            fileName,
            displayName,
            () => LoadResourceDocument(fileName, displayName, assemblyName));
    }

    private static FlowDocument LoadResourceDocument(string fileName, string displayName, string assemblyName)
    {
        var assemblySegment = Uri.EscapeDataString(assemblyName);
        var uri = new Uri(
            $"pack://application:,,,/{assemblySegment};component/Help/Topics/{fileName}.xaml",
            UriKind.Absolute);

        var resource = Application.GetResourceStream(uri);
        if (resource?.Stream == null)
        {
            return HelpContentBuilder.Create(
                displayName,
                "This help topic could not be found in the editor resources.",
                new HelpSection("Missing topic", $"Expected resource: Help/Topics/{fileName}.xaml"));
        }

        string xamlText;
        using (var reader = new StreamReader(resource.Stream))
        {
            xamlText = reader.ReadToEnd();
        }

        xamlText = xamlText.TrimStart('\uFEFF', '\u200B');
        xamlText = Regex.Replace(xamlText, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        xamlText = xamlText.TrimStart();

        return (FlowDocument)XamlReader.Parse(xamlText);
    }
}
