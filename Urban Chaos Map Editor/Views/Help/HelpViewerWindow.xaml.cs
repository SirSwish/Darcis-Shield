// /Views/Help/HelpViewerWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace UrbanChaosMapEditor.Views.Help
{
    public partial class HelpViewerWindow : Window
    {
        private readonly ObservableCollection<HelpTopic> _allTopics = new();
        private readonly ObservableCollection<HelpTopic> _filteredTopics = new();

        public HelpViewerWindow()
        {
            InitializeComponent();
            TopicList.ItemsSource = _filteredTopics;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadTopicIndex();

            if (_filteredTopics.Count > 0)
                TopicList.SelectedIndex = 0;
        }

        /// <summary>
        /// Registers all help topics in display order.
        /// Add new topics here as they are created.
        /// </summary>
        private void LoadTopicIndex()
        {
            _allTopics.Clear();

            // ---- REGISTER TOPICS HERE ----
            // Order determines sidebar display order.
            // FileName must match the .xaml file in Help/Topics/ (without extension).

            Register("Getting_Started", "Getting Started");
            Register("Map_Overview", "Map Overview");
            Register("Textures", "Textures & Painting");
            Register("Heights", "Heights & Terrain");
            Register("Buildings_Overview", "Buildings — Overview");
            Register("Building_Walls", "Buildings — Walls");
            Register("Building_Warehouses", "Buildings — Warehouses");
            Register("Building_Doors", "Buildings — Doors & Fences");
            Register("Roofs_Walkables", "Roofs & Walkables");
            Register("Prims_Objects", "Prims & Objects");
            Register("Styles_TMA", "Styles & TMA System");
            Register("Facet_Painting", "Facet Painting");
            Register("Keyboard_Shortcuts", "Keyboard Shortcuts");
            Register("File_Format", "IAM File Format Reference");
            Register("Troubleshooting", "Troubleshooting");

            // ---- END TOPIC REGISTRATION ----

            ApplyFilter("");
        }

        private void Register(string fileName, string displayName)
        {
            _allTopics.Add(new HelpTopic
            {
                FileName = fileName,
                DisplayName = displayName
            });
        }

        private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TopicList.SelectedItem is HelpTopic topic)
                LoadTopic(topic);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(TxtSearch.Text ?? "");
        }

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
                string packUri = $"pack://application:,,,/UrbanChaosMapEditor;component/Help/Topics/{topic.FileName}.xaml";
                var sri = Application.GetResourceStream(new Uri(packUri));

                if (sri?.Stream == null)
                {
                    ShowPlaceholder(topic.DisplayName);
                    return;
                }

                // Read the stream as text, stripping BOM and invalid XML characters
                string xamlText;
                using (var reader = new System.IO.StreamReader(sri.Stream))
                {
                    xamlText = reader.ReadToEnd();
                }

                // Strip BOM and control characters
                xamlText = xamlText.TrimStart('\uFEFF', '\u200B');
                xamlText = System.Text.RegularExpressions.Regex.Replace(
                    xamlText, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
                xamlText = xamlText.TrimStart();

                var doc = (FlowDocument)XamlReader.Parse(xamlText);

                DocViewer.Document = doc;
                TxtBreadcrumb.Text = $"Help  >  {topic.DisplayName}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HelpViewer] Failed to load topic '{topic.FileName}': {ex.Message}");
                ShowPlaceholder(topic.DisplayName);
            }
        }

        private void ShowPlaceholder(string topicName)
        {
            var doc = new FlowDocument
            {
                Background = (Brush)FindResource("HelpBackground"),
                Foreground = (Brush)FindResource("HelpForeground"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                PagePadding = new Thickness(32, 24, 32, 24)
            };

            doc.Blocks.Add(new Paragraph(new Run(topicName))
            {
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
            });

            doc.Blocks.Add(new Paragraph(new Run("This help topic is not yet written."))
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 8, 0, 0)
            });

            DocViewer.Document = doc;
            TxtBreadcrumb.Text = $"Help  ›  {topicName}  (coming soon)";
        }

        /// <summary>
        /// Open the help window, optionally jumping to a specific topic.
        /// </summary>
        public static void ShowHelp(string? topicFileName = null, Window? owner = null)
        {
            var win = new HelpViewerWindow();
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

    public sealed class HelpTopic : INotifyPropertyChanged
    {
        public string FileName { get; init; } = "";
        public string DisplayName { get; init; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}