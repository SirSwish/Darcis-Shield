using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UrbanChaosMissionEditor.Controls;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Cutscene (WPT_CUT_SCENE / WaypointType.CutScene) EventPoints.
/// Integrates the TimelineControl for visual editing of cutscene sequences.
/// </summary>
public partial class CutsceneEditorControl : UserControl
{
    private CutscenePacket? _selectedPacket;
    private CutsceneChannel? _selectedChannel;

    public CutsceneEditorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private EventPointEditorViewModel? ViewModel => DataContext as EventPointEditorViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        var cutscene = ViewModel?.Cutscene;

        if (cutscene == null)
        {
            EmptyStateOverlay.Visibility = Visibility.Visible;
            InitButton.Visibility = Visibility.Visible;
            BtnAddChannel.IsEnabled = false;
            BtnAddPacket.IsEnabled = false;
            VersionText.Text = "N/A";
            ChannelCountText.Text = "0";
            DurationText.Text = "0 frames";
            PlayheadText.Text = "0";

            // Clear timeline if cutscene was removed
            if (Timeline.Cutscene != null)
            {
                Timeline.Cutscene = null;
            }
        }
        else
        {
            EmptyStateOverlay.Visibility = Visibility.Collapsed;
            InitButton.Visibility = Visibility.Collapsed;
            BtnAddChannel.IsEnabled = true;
            BtnAddPacket.IsEnabled = _selectedChannel != null;

            VersionText.Text = cutscene.Version.ToString();
            ChannelCountText.Text = cutscene.Channels.Count.ToString();
            DurationText.Text = $"{cutscene.Duration} frames";
            PlayheadText.Text = Timeline.CurrentFrame.ToString();

            // ONLY set Timeline.Cutscene if it changed
            // This avoids redundant refreshes that can cause alignment issues
            if (Timeline.Cutscene != cutscene)
            {
                Timeline.Cutscene = cutscene;
            }
            // If cutscene is the same object, RefreshTimeline was already called
            // by whatever modified the cutscene data
        }
    }

    // ========================================
    // Timeline Event Handlers
    // ========================================

    private void Timeline_PacketSelected(object? sender, CutscenePacket? packet)
    {
        _selectedPacket = packet;
        _selectedChannel = Timeline.SelectedChannel;
        BtnAddPacket.IsEnabled = _selectedChannel != null;
        UpdatePropertiesPanel();
    }

    private void Timeline_ChannelSelected(object? sender, CutsceneChannel? channel)
    {
        _selectedChannel = channel;
        _selectedPacket = null;
        BtnAddPacket.IsEnabled = channel != null;
        UpdatePropertiesPanel();
    }

    private void Timeline_PlayheadMoved(object? sender, int frame)
    {
        PlayheadText.Text = frame.ToString();
    }

    private void Timeline_CutsceneModified(object? sender, EventArgs e)
    {
        // Update summary when cutscene is modified
        if (ViewModel?.Cutscene != null)
        {
            DurationText.Text = $"{ViewModel.Cutscene.Duration} frames";
        }
    }

    // ========================================
    // Button Handlers
    // ========================================

    private void InitializeCutscene_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.EnsureCutsceneData();
        RefreshUI();
    }

    private void AddChannel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Cutscene == null)
        {
            ViewModel?.EnsureCutsceneData();
            // After initializing, set the timeline cutscene
            Timeline.Cutscene = ViewModel?.Cutscene;
        }

        var dialog = new AddChannelDialog();
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true)
        {
            ViewModel?.AddCutsceneChannel(dialog.SelectedChannelType, dialog.SelectedPersonType);
            Timeline.RefreshTimeline();

            // Update UI text without re-setting Timeline.Cutscene
            UpdateSummaryText();
        }
    }

    private void AddPacket_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChannel == null)
        {
            MessageBox.Show("Please select a channel first.", "Add Packet",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Create packet at current playhead position
        var packet = new CutscenePacket
        {
            Type = _selectedChannel.Type switch
            {
                CutsceneChannelType.Character => CutscenePacketType.Animation,
                CutsceneChannelType.Camera => CutscenePacketType.Camera,
                CutsceneChannelType.Sound => CutscenePacketType.Sound,
                CutsceneChannelType.Subtitles => CutscenePacketType.Text,
                _ => CutscenePacketType.Unused
            },
            Start = (ushort)Timeline.CurrentFrame,
            Length = 30
        };

        _selectedChannel.Packets.Add(packet);
        Timeline.RefreshTimeline();
        Timeline.SelectPacket(packet, _selectedChannel);

        // Update UI text without re-setting Timeline.Cutscene
        UpdateSummaryText();
    }

    // Add this helper method:
    private void UpdateSummaryText()
    {
        var cutscene = ViewModel?.Cutscene;
        if (cutscene != null)
        {
            VersionText.Text = cutscene.Version.ToString();
            ChannelCountText.Text = cutscene.Channels.Count.ToString();
            DurationText.Text = $"{cutscene.Duration} frames";
            PlayheadText.Text = Timeline.CurrentFrame.ToString();
        }
    }
    // ========================================
    // Properties Panel
    // ========================================

    private void UpdatePropertiesPanel()
    {
        SelectedItemPropertiesContainer.Children.Clear();

        if (_selectedPacket != null)
        {
            ShowPacketProperties(_selectedPacket);
        }
        else if (_selectedChannel != null)
        {
            ShowChannelProperties(_selectedChannel);
        }
        else
        {
            SelectedItemPropertiesContainer.Children.Add(new TextBlock
            {
                Text = "Select a channel or packet",
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                FontStyle = FontStyles.Italic
            });
            SelectedItemPropertiesContainer.Children.Add(new TextBlock
            {
                Text = "to view and edit its properties",
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                FontSize = 11
            });
        }
    }

    private void ShowChannelProperties(CutsceneChannel channel)
    {
        // Header
        SelectedItemPropertiesContainer.Children.Add(new TextBlock
        {
            Text = "Channel Properties",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        AddPropertyRow("Type:", channel.Type.ToString());
        AddPropertyRow("Name:", channel.DisplayName);
        AddPropertyRow("Packets:", channel.Packets.Count.ToString());

        if (channel.Type == CutsceneChannelType.Character)
        {
            AddPropertyRow("Person ID:", channel.Index.ToString());

            // Person type combo
            AddComboRow("Character:", GetPersonTypes(), (int)channel.Index - 1, index =>
            {
                channel.Index = (ushort)(index + 1);
                Timeline.RefreshTimeline();
            });
        }

        // Delete channel button
        var deleteButton = new Button
        {
            Content = "Delete Channel",
            Margin = new Thickness(0, 15, 0, 0),
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(0x8B, 0x3A, 0x3A))
        };
        deleteButton.Click += (s, e) =>
        {
            var result = MessageBox.Show($"Delete channel '{channel.DisplayName}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                ViewModel?.Cutscene?.Channels.Remove(channel);
                _selectedChannel = null;
                _selectedPacket = null;
                Timeline.RefreshTimeline();
                RefreshUI();
                UpdatePropertiesPanel();
            }
        };
        SelectedItemPropertiesContainer.Children.Add(deleteButton);
    }

    private void ShowPacketProperties(CutscenePacket packet)
    {
        // Header
        SelectedItemPropertiesContainer.Children.Add(new TextBlock
        {
            Text = "Packet Properties",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        AddPropertyRow("Type:", packet.Type.ToString());

        // Timing
        AddSection("Timing");
        AddEditableRow("Start:", packet.Start.ToString(), v =>
        {
            if (ushort.TryParse(v, out var val))
            {
                packet.Start = val;
                Timeline.RefreshTimeline();
            }
        });
        AddEditableRow("Length:", packet.Length.ToString(), v =>
        {
            if (ushort.TryParse(v, out var val))
            {
                packet.Length = val;
                Timeline.RefreshTimeline();
            }
        });

        // Index (for animations/sounds)
        if (packet.Type == CutscenePacketType.Animation && _selectedChannel != null)
        {
            string animName = AnimationNameTable.GetAnimationName(
                _selectedChannel.Index,
                packet.Index
            );
            AddPropertyRow("Animation:", animName);

            // Add a combo box to select animation
            AddAnimationComboRow(packet);
        }

        // Position section
        AddSection("Position");
        AddEditableRow("X:", packet.PosX.ToString(), v => { if (int.TryParse(v, out var val)) packet.PosX = val; });
        AddEditableRow("Y:", packet.PosY.ToString(), v => { if (int.TryParse(v, out var val)) packet.PosY = val; });
        AddEditableRow("Z:", packet.PosZ.ToString(), v => { if (int.TryParse(v, out var val)) packet.PosZ = val; });

        // Rotation section
        AddSection("Rotation");
        AddEditableRow("Angle:", packet.Angle.ToString(), v => { if (ushort.TryParse(v, out var val)) packet.Angle = val; });
        AddEditableRow("Pitch:", packet.Pitch.ToString(), v => { if (ushort.TryParse(v, out var val)) packet.Pitch = val; });

        // Camera-specific properties
        if (packet.Type == CutscenePacketType.Camera)
        {
            AddSection("Camera Settings");
            AddEditableRow("Lens:", packet.CameraLens.ToString(), v =>
            {
                if (byte.TryParse(v, out var val)) packet.CameraLens = val;
            });
            AddEditableRow("Fade:", packet.CameraFade.ToString(), v =>
            {
                if (byte.TryParse(v, out var val)) packet.CameraFade = val;
            });
        }

        // Text for subtitles
        if (packet.Type == CutscenePacketType.Text)
        {
            AddSection("Subtitle Text");
            var textBox = new TextBox
            {
                Text = packet.Text ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 60
            };
            textBox.TextChanged += (s, e) =>
            {
                packet.Text = textBox.Text;
                Timeline.RefreshTimeline();
            };
            SelectedItemPropertiesContainer.Children.Add(textBox);
        }

        // Interpolation flags
        AddSection("Interpolation");
        AddCheckboxRow("Interpolate Position", packet.InterpolatePosition, v => packet.InterpolatePosition = v);
        AddCheckboxRow("Interpolate Rotation", packet.InterpolateRotation, v => packet.InterpolateRotation = v);

        // Additional flags
        AddSection("Flags");
        AddCheckboxRow("Backwards / Securicam", packet.Flags.HasFlag(PacketFlags.Backwards), v =>
        {
            if (v) packet.Flags |= PacketFlags.Backwards;
            else packet.Flags &= ~PacketFlags.Backwards;
        });
        AddCheckboxRow("Slow Motion", packet.Flags.HasFlag(PacketFlags.SlowMotion), v =>
        {
            if (v) packet.Flags |= PacketFlags.SlowMotion;
            else packet.Flags &= ~PacketFlags.SlowMotion;
        });

        // Move interpolation
        AddComboRow("Position Easing:", new[] { "None", "Smooth In", "Smooth Out", "Smooth Both" },
            GetPositionEasingIndex(packet), index => SetPositionEasing(packet, index));

        AddComboRow("Rotation Easing:", new[] { "None", "Smooth In", "Smooth Out", "Smooth Both" },
            GetRotationEasingIndex(packet), index => SetRotationEasing(packet, index));

        // Delete button
        var deleteButton = new Button
        {
            Content = "Delete Packet",
            Margin = new Thickness(0, 15, 0, 0),
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(0x8B, 0x3A, 0x3A))
        };
        deleteButton.Click += (s, e) =>
        {
            if (_selectedChannel != null)
            {
                _selectedChannel.Packets.Remove(packet);
                _selectedPacket = null;
                Timeline.RefreshTimeline();
                UpdatePropertiesPanel();
            }
        };
        SelectedItemPropertiesContainer.Children.Add(deleteButton);
    }

    private void AddAnimationComboRow(CutscenePacket packet)
    {
        if (_selectedChannel == null) return;

        var animations = AnimationNameTable.GetAnimationsForPerson(_selectedChannel.Index);

        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = "Select:",
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);

        var combo = new ComboBox { MaxWidth = 150, HorizontalAlignment = HorizontalAlignment.Left };

        // Add animations to combo
        foreach (var kvp in animations.OrderBy(x => x.Key))
        {
            var item = new ComboBoxItem
            {
                Content = $"{kvp.Key}: {kvp.Value}",
                Tag = kvp.Key
            };
            combo.Items.Add(item);

            if (kvp.Key == packet.Index)
                combo.SelectedItem = item;
        }

        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedItem is ComboBoxItem selected && selected.Tag is int index)
            {
                packet.Index = (ushort)index;
                Timeline.RefreshTimeline();
            }
        };

        Grid.SetColumn(combo, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(combo);
        SelectedItemPropertiesContainer.Children.Add(grid);
    }

    // ========================================
    // Helper Methods for Properties Panel
    // ========================================

    private void AddSection(string title)
    {
        SelectedItemPropertiesContainer.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 10, 0, 5)
        });
    }

    private void AddPropertyRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };
        Grid.SetColumn(labelBlock, 0);

        var valueBlock = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
        };
        Grid.SetColumn(valueBlock, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        SelectedItemPropertiesContainer.Children.Add(grid);
    }

    private void AddEditableRow(string label, string value, Action<string> onChanged)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);

        var textBox = new TextBox
        {
            Text = value,
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        textBox.LostFocus += (s, e) => onChanged(textBox.Text);
        textBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) onChanged(textBox.Text); };
        Grid.SetColumn(textBox, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(textBox);
        SelectedItemPropertiesContainer.Children.Add(grid);
    }

    private void AddCheckboxRow(string label, bool isChecked, Action<bool> onChanged)
    {
        var checkBox = new CheckBox
        {
            Content = label,
            IsChecked = isChecked,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 2, 0, 2)
        };
        checkBox.Checked += (s, e) => onChanged(true);
        checkBox.Unchecked += (s, e) => onChanged(false);
        SelectedItemPropertiesContainer.Children.Add(checkBox);
    }

    private void AddComboRow(string label, string[] options, int selectedIndex, Action<int> onChanged)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);

        var combo = new ComboBox { Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var option in options)
            combo.Items.Add(option);

        if (selectedIndex >= 0 && selectedIndex < options.Length)
            combo.SelectedIndex = selectedIndex;

        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex >= 0)
                onChanged(combo.SelectedIndex);
        };
        Grid.SetColumn(combo, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(combo);
        SelectedItemPropertiesContainer.Children.Add(grid);
    }

    private static string[] GetPersonTypes() => new[]
    {
        "Darci", "Roper", "Cop", "Civilian", "Rasta Thug",
        "Grey Thug", "Red Thug", "Prostitute", "Fat Prostitute",
        "Hostage", "Mechanic", "Tramp", "MIB 1", "MIB 2", "MIB 3"
    };

    private static int GetPositionEasingIndex(CutscenePacket packet)
    {
        if (!packet.InterpolatePosition) return 0;
        bool smoothIn = packet.Flags.HasFlag(PacketFlags.SmoothMoveIn);
        bool smoothOut = packet.Flags.HasFlag(PacketFlags.SmoothMoveOut);
        if (smoothIn && smoothOut) return 3;
        if (smoothIn) return 1;
        if (smoothOut) return 2;
        return 0;
    }

    private static void SetPositionEasing(CutscenePacket packet, int index)
    {
        packet.Flags &= ~(PacketFlags.SmoothMoveIn | PacketFlags.SmoothMoveOut);
        switch (index)
        {
            case 1: packet.Flags |= PacketFlags.SmoothMoveIn; break;
            case 2: packet.Flags |= PacketFlags.SmoothMoveOut; break;
            case 3: packet.Flags |= PacketFlags.SmoothMoveIn | PacketFlags.SmoothMoveOut; break;
        }
    }

    private static int GetRotationEasingIndex(CutscenePacket packet)
    {
        if (!packet.InterpolateRotation) return 0;
        bool smoothIn = packet.Flags.HasFlag(PacketFlags.SmoothRotIn);
        bool smoothOut = packet.Flags.HasFlag(PacketFlags.SmoothRotOut);
        if (smoothIn && smoothOut) return 3;
        if (smoothIn) return 1;
        if (smoothOut) return 2;
        return 0;
    }

    private static void SetRotationEasing(CutscenePacket packet, int index)
    {
        packet.Flags &= ~(PacketFlags.SmoothRotIn | PacketFlags.SmoothRotOut);
        switch (index)
        {
            case 1: packet.Flags |= PacketFlags.SmoothRotIn; break;
            case 2: packet.Flags |= PacketFlags.SmoothRotOut; break;
            case 3: packet.Flags |= PacketFlags.SmoothRotIn | PacketFlags.SmoothRotOut; break;
        }
    }
}

/// <summary>
/// Dialog for selecting channel type when adding a new channel
/// </summary>
public class AddChannelDialog : Window
{
    private ComboBox _typeCombo;
    private ComboBox _personCombo;
    private StackPanel _personPanel;

    public CutsceneChannelType SelectedChannelType { get; private set; }
    public ushort SelectedPersonType { get; private set; } = 1;

    public AddChannelDialog()
    {
        Title = "Add Channel";
        Width = 300;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));

        var grid = new Grid { Margin = new Thickness(15) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Channel type label
        var typeLabel = new TextBlock
        {
            Text = "Channel Type:",
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 5)
        };
        Grid.SetRow(typeLabel, 0);
        grid.Children.Add(typeLabel);

        // Channel type combo
        _typeCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
        _typeCombo.Items.Add("Character");
        _typeCombo.Items.Add("Camera");
        _typeCombo.Items.Add("Sound");
        _typeCombo.Items.Add("Subtitles");
        _typeCombo.SelectedIndex = 0;
        _typeCombo.SelectionChanged += TypeCombo_SelectionChanged;
        Grid.SetRow(_typeCombo, 1);
        grid.Children.Add(_typeCombo);

        // Person type panel
        _personPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var personLabel = new TextBlock
        {
            Text = "Character Type:",
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 5)
        };
        _personCombo = new ComboBox();
        foreach (var person in new[] { "Darci", "Roper", "Cop", "Civilian", "Rasta Thug", "Grey Thug", "Red Thug" })
            _personCombo.Items.Add(person);
        _personCombo.SelectedIndex = 0;
        _personPanel.Children.Add(personLabel);
        _personPanel.Children.Add(_personCombo);
        Grid.SetRow(_personPanel, 2);
        grid.Children.Add(_personPanel);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okButton = new Button
        {
            Content = "Add",
            Width = 75,
            Margin = new Thickness(0, 0, 10, 0),
            IsDefault = true,
            Padding = new Thickness(5)
        };
        okButton.Click += OkButton_Click;
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 75,
            IsCancel = true,
            Padding = new Thickness(5)
        };
        buttonPanel.Children.Add(cancelButton);

        Grid.SetRow(buttonPanel, 3);
        grid.Children.Add(buttonPanel);

        Content = grid;
    }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _personPanel.Visibility = _typeCombo.SelectedIndex == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedChannelType = _typeCombo.SelectedIndex switch
        {
            0 => CutsceneChannelType.Character,
            1 => CutsceneChannelType.Camera,
            2 => CutsceneChannelType.Sound,
            3 => CutsceneChannelType.Subtitles,
            _ => CutsceneChannelType.Character
        };
        SelectedPersonType = (ushort)(_personCombo.SelectedIndex + 1);
        DialogResult = true;
    }
}