using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UrbanChaosLightEditor.Models;
using UrbanChaosLightEditor.Services;

namespace UrbanChaosLightEditor.Views
{
    public partial class LightPropertiesPanel : UserControl
    {
        private readonly LightsAccessor _acc;
        private LightProperties _props;
        private LightNightColour _nightColour;
        private bool _isLoading = true;

        public LightPropertiesPanel()
        {
            InitializeComponent();
            _acc = new LightsAccessor(LightsDataService.Instance);

            // Subscribe to lights loaded event to refresh
            LightsDataService.Instance.LightsLoaded += (_, __) => Dispatcher.Invoke(LoadProperties);
            LightsDataService.Instance.LightsCleared += (_, __) => Dispatcher.Invoke(ClearUI);

            Loaded += (_, __) =>
            {
                if (LightsDataService.Instance.IsLoaded)
                    LoadProperties();
            };
        }

        private void ClearUI()
        {
            _isLoading = true;

            // Reset all sliders to 0
            SliderD3DAlpha.Value = 0;
            SliderD3DRed.Value = 0;
            SliderD3DGreen.Value = 0;
            SliderD3DBlue.Value = 0;
            SliderSpecAlpha.Value = 0;
            SliderSpecRed.Value = 0;
            SliderSpecGreen.Value = 0;
            SliderSpecBlue.Value = 0;
            SliderAmbRed.Value = 0;
            SliderAmbGreen.Value = 0;
            SliderAmbBlue.Value = 0;
            SliderLampRed.Value = 128;
            SliderLampGreen.Value = 128;
            SliderLampBlue.Value = 128;
            SliderLampRadius.Value = 0;
            SliderSkyRed.Value = 0;
            SliderSkyGreen.Value = 0;
            SliderSkyBlue.Value = 0;

            // Reset checkboxes
            ChkNightEnabled.IsChecked = false;
            ChkLampsOn.IsChecked = false;
            ChkDarkenWalls.IsChecked = false;
            ChkDay.IsChecked = false;

            _isLoading = false;
        }

        private void LoadProperties()
        {
            if (!LightsDataService.Instance.IsLoaded) return;

            _isLoading = true;

            try
            {
                _props = _acc.ReadProperties();
                _nightColour = _acc.ReadNightColour();

                Debug.WriteLine($"[LightPropertiesPanel.Load] D3D=0x{_props.NightAmbD3DColour:X8}, NightFlag=0x{_props.NightFlag:X8}");

                // D3D Color
                SliderD3DAlpha.Value = _props.D3DAlpha;
                SliderD3DRed.Value = _props.D3DRed;
                SliderD3DGreen.Value = _props.D3DGreen;
                SliderD3DBlue.Value = _props.D3DBlue;

                // Specular
                SliderSpecAlpha.Value = _props.SpecularAlpha;
                SliderSpecRed.Value = _props.SpecularRed;
                SliderSpecGreen.Value = _props.SpecularGreen;
                SliderSpecBlue.Value = _props.SpecularBlue;

                // Ambient
                SliderAmbRed.Value = _props.NightAmbRed;
                SliderAmbGreen.Value = _props.NightAmbGreen;
                SliderAmbBlue.Value = _props.NightAmbBlue;

                // Lamppost (sbyte shifted to 0-255)
                SliderLampRed.Value = _props.NightLampostRed + 128;
                SliderLampGreen.Value = _props.NightLampostGreen + 128;
                SliderLampBlue.Value = _props.NightLampostBlue + 128;
                SliderLampRadius.Value = _props.NightLampostRadius;

                // Sky
                SliderSkyRed.Value = _nightColour.Red;
                SliderSkyGreen.Value = _nightColour.Green;
                SliderSkyBlue.Value = _nightColour.Blue;

                // Night flags (bits: 0=Night, 1=LampsOn, 2=DarkenWalls, 3=Day)
                ChkNightEnabled.IsChecked = (_props.NightFlag & 0x01) != 0;
                ChkLampsOn.IsChecked = (_props.NightFlag & 0x02) != 0;
                ChkDarkenWalls.IsChecked = (_props.NightFlag & 0x04) != 0;
                ChkDay.IsChecked = (_props.NightFlag & 0x08) != 0;

                UpdateAllPreviews();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LightPropertiesPanel.Load] ERROR: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnPropertyChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            UpdateAllPreviews();
        }

        private void UpdateAllPreviews()
        {
            // D3D Preview
            byte d3dA = (byte)SliderD3DAlpha.Value;
            byte d3dR = (byte)SliderD3DRed.Value;
            byte d3dG = (byte)SliderD3DGreen.Value;
            byte d3dB = (byte)SliderD3DBlue.Value;
            D3DPreview.Fill = new SolidColorBrush(Color.FromArgb(d3dA, d3dR, d3dG, d3dB));

            // Specular Preview
            byte specA = (byte)SliderSpecAlpha.Value;
            byte specR = (byte)SliderSpecRed.Value;
            byte specG = (byte)SliderSpecGreen.Value;
            byte specB = (byte)SliderSpecBlue.Value;
            SpecPreview.Fill = new SolidColorBrush(Color.FromArgb(specA, specR, specG, specB));

            // Ambient Preview (shift for display)
            byte ambR = (byte)Math.Clamp((int)SliderAmbRed.Value + 128, 0, 255);
            byte ambG = (byte)Math.Clamp((int)SliderAmbGreen.Value + 128, 0, 255);
            byte ambB = (byte)Math.Clamp((int)SliderAmbBlue.Value + 128, 0, 255);
            AmbientPreview.Fill = new SolidColorBrush(Color.FromRgb(ambR, ambG, ambB));

            // Lamp Preview
            byte lampR = (byte)SliderLampRed.Value;
            byte lampG = (byte)SliderLampGreen.Value;
            byte lampB = (byte)SliderLampBlue.Value;
            LampPreview.Fill = new SolidColorBrush(Color.FromRgb(lampR, lampG, lampB));

            // Sky Preview
            byte skyR = (byte)SliderSkyRed.Value;
            byte skyG = (byte)SliderSkyGreen.Value;
            byte skyB = (byte)SliderSkyBlue.Value;
            SkyPreview.Fill = new SolidColorBrush(Color.FromRgb(skyR, skyG, skyB));
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!LightsDataService.Instance.IsLoaded) return;

            try
            {
                Debug.WriteLine("[LightPropertiesPanel.Apply] Saving properties...");

                // Build packed colors
                uint d3dColor = ((uint)(byte)SliderD3DAlpha.Value << 24) |
                                ((uint)(byte)SliderD3DRed.Value << 16) |
                                ((uint)(byte)SliderD3DGreen.Value << 8) |
                                (byte)SliderD3DBlue.Value;

                uint specColor = ((uint)(byte)SliderSpecAlpha.Value << 24) |
                                 ((uint)(byte)SliderSpecRed.Value << 16) |
                                 ((uint)(byte)SliderSpecGreen.Value << 8) |
                                 (byte)SliderSpecBlue.Value;

                // Build night flags from checkboxes
                uint nightFlag = 0;
                if (ChkNightEnabled.IsChecked == true) nightFlag |= 0x01;
                if (ChkLampsOn.IsChecked == true) nightFlag |= 0x02;
                if (ChkDarkenWalls.IsChecked == true) nightFlag |= 0x04;
                if (ChkDay.IsChecked == true) nightFlag |= 0x08;

                Debug.WriteLine($"[LightPropertiesPanel.Apply] NightFlag=0x{nightFlag:X2}");

                var newProps = new LightProperties
                {
                    EdLightFree = _props.EdLightFree,
                    NightFlag = nightFlag,
                    NightAmbD3DColour = d3dColor,
                    NightAmbD3DSpecular = specColor,
                    NightAmbRed = (int)SliderAmbRed.Value,
                    NightAmbGreen = (int)SliderAmbGreen.Value,
                    NightAmbBlue = (int)SliderAmbBlue.Value,
                    NightLampostRed = (sbyte)((int)SliderLampRed.Value - 128),
                    NightLampostGreen = (sbyte)((int)SliderLampGreen.Value - 128),
                    NightLampostBlue = (sbyte)((int)SliderLampBlue.Value - 128),
                    Padding = _props.Padding,
                    NightLampostRadius = (int)SliderLampRadius.Value,
                };

                var newNightColour = new LightNightColour
                {
                    Red = (byte)SliderSkyRed.Value,
                    Green = (byte)SliderSkyGreen.Value,
                    Blue = (byte)SliderSkyBlue.Value,
                };

                _acc.WriteProperties(newProps);
                _acc.WriteNightColour(newNightColour);

                // Refresh our cached copy
                _props = newProps;
                _nightColour = newNightColour;

                Debug.WriteLine("[LightPropertiesPanel.Apply] Done!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LightPropertiesPanel.Apply] ERROR: {ex.Message}");
                MessageBox.Show($"Failed to apply properties:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}