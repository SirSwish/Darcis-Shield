using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UrbanChaosEditor.Shared.Views.Help;

public partial class HelpViewerWindow : Window
{
    private readonly ObservableCollection<HelpTopic> _allTopics = new();
    private readonly ObservableCollection<HelpTopic> _filteredTopics = new();

    public HelpViewerWindow(string title, IEnumerable<HelpTopic> topics)
    {
        InitializeComponent();
        Title = title;
        TopicList.ItemsSource = _filteredTopics;

        foreach (var topic in topics)
            _allTopics.Add(topic);

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyFilter("");
        var firstTopic = FindFirstDocumentTopic(_filteredTopics);
        if (firstTopic != null)
            LoadTopic(firstTopic);
    }

    private void TopicList_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is HelpTopic topic)
            LoadTopic(topic);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(TxtSearch.Text ?? "");

    private void ApplyFilter(string query)
    {
        _filteredTopics.Clear();
        var q = query.Trim();

        foreach (var topic in _allTopics)
        {
            var filteredTopic = FilterTopic(topic, q);
            if (filteredTopic != null)
                _filteredTopics.Add(filteredTopic);
        }
    }

    private static HelpTopic? FilterTopic(HelpTopic topic, string query)
    {
        if (string.IsNullOrEmpty(query))
            return topic;

        var topicMatches = topic.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
        var childMatches = topic.Children
            .Select(child => FilterTopic(child, query))
            .Where(child => child != null)
            .Cast<HelpTopic>()
            .ToList();

        if (topicMatches)
            return topic;

        return childMatches.Count > 0 ? topic.WithChildren(childMatches) : null;
    }

    private void LoadTopic(HelpTopic topic)
    {
        try
        {
            if (!topic.HasDocument)
            {
                var firstTopic = FindFirstDocumentTopic(topic.Children);
                if (firstTopic != null)
                    LoadTopic(firstTopic);
                return;
            }

            DocViewer.Document = topic.CreateDocument!();
            TxtBreadcrumb.Text = $"Help  >  {topic.DisplayName}";
            ScrollDocToTop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HelpViewer] Failed to load topic '{topic.FileName}': {ex.Message}");
            DocViewer.Document = HelpContentBuilder.Create(
                topic.DisplayName,
                "This help topic could not be loaded.",
                new HelpSection("Error", ex.Message));
            TxtBreadcrumb.Text = $"Help  >  {topic.DisplayName}";
        }
    }

    private static HelpTopic? FindFirstDocumentTopic(IEnumerable<HelpTopic> topics)
    {
        foreach (var topic in topics)
        {
            if (topic.HasDocument)
                return topic;

            var child = FindFirstDocumentTopic(topic.Children);
            if (child != null)
                return child;
        }

        return null;
    }

    private HelpTopic? FindTopicByFileName(string topicFileName)
        => FindTopicByFileName(_filteredTopics, topicFileName);

    private static HelpTopic? FindTopicByFileName(IEnumerable<HelpTopic> topics, string topicFileName)
    {
        foreach (var topic in topics)
        {
            if (topic.FileName.Equals(topicFileName, StringComparison.OrdinalIgnoreCase))
                return topic;

            var child = FindTopicByFileName(topic.Children, topicFileName);
            if (child != null)
                return child;
        }

        return null;
    }

    private void ScrollDocToTop()
    {
        DocViewer.ApplyTemplate();
        var sv = FindVisualChild<ScrollViewer>(DocViewer);
        sv?.ScrollToTop();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    public static void ShowHelp(string title, IEnumerable<HelpTopic> topics, Window? owner = null, string? topicFileName = null)
    {
        var win = new HelpViewerWindow(title, topics);
        if (owner != null)
            win.Owner = owner;

        win.Show();

        if (!string.IsNullOrEmpty(topicFileName))
        {
            var match = win.FindTopicByFileName(topicFileName);

            if (match != null)
                win.LoadTopic(match);
        }
    }
}
