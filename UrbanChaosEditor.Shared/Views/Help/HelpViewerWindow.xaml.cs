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
        if (_filteredTopics.Count > 0)
            TopicList.SelectedIndex = 0;
    }

    private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicList.SelectedItem is HelpTopic topic)
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
            if (string.IsNullOrEmpty(q) ||
                topic.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                _filteredTopics.Add(topic);
            }
        }
    }

    private void LoadTopic(HelpTopic topic)
    {
        try
        {
            DocViewer.Document = topic.CreateDocument();
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
            var match = win._filteredTopics.FirstOrDefault(
                t => t.FileName.Equals(topicFileName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
                win.TopicList.SelectedItem = match;
        }
    }
}
