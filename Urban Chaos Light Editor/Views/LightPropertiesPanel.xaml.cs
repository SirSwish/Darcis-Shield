// /Views/LightPropertiesPanel.xaml.cs
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

            ChkNightEnabled.IsChecked = false;
            ChkLampsOn.IsChecked = false;
            ChkDarkenWalls.IsChecked = false;

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

                Debug.WriteLine($"[LightPropertiesPanel.Load] NightFlag=0x{_props.NightFlag:X8}, D3D=0x{_props.NightAmbD3DColour:X8}");

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

                // Ambient — expand engine-scaled internal value to UI 0-255: ui = internal * 820 / 256
                SliderAmbRed.Value   = Math.Clamp(_props.NightAmbRed   * 820 / 256, 0, 255);
                SliderAmbGreen.Value = Math.Clamp(_props.NightAmbGreen * 820 / 256, 0, 255);
                SliderAmbBlue.Value  = Math.Clamp(_props.NightAmbBlue  * 820 / 256, 0, 255);

                // Lamppost (sbyte shifted to 0-255)
                SliderLampRed.Value = _props.NightLampostRed + 128;
                SliderLampGreen.Value = _props.NightLampostGreen + 128;
                SliderLampBlue.Value = _props.NightLampostBlue + 128;
                SliderLampRadius.Value = _props.NightLampostRadius;

                // Sky
                SliderSkyRed.Value = _nightColour.Red;
                SliderSkyGreen.Value = _nightColour.Green;
                SliderSkyBlue.Value = _nightColour.Blue;

                // DAYTIME bit (bit 2): SET = daytime, CLEAR = night. Night checkbox = inverse of DAYTIME bit.
                bool isDaytime = (_props.NightFlag & LightsAccessor.NIGHT_FLAG_DAYTIME) != 0;
                ChkNightEnabled.IsChecked = !isDaytime;
                ChkLampsOn.IsChecked = (_props.NightFlag & LightsAccessor.NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS) != 0;
                ChkDarkenWalls.IsChecked = (_props.NightFlag & LightsAccessor.NIGHT_FLAG_DARKEN_BUILDING_POINTS) != 0;

                Debug.WriteLine($"[LightPropertiesPanel.Load] Parsed: IsNight={!isDaytime}, LampsOn={ChkLampsOn.IsChecked}, DarkenWalls={ChkDarkenWalls.IsChecked}");

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
            ApplyChanges();
        }

        private void OnCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            ApplyChanges();
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

            AmbientPreview.Fill = new SolidColorBrush(Color.FromRgb(
                (byte)SliderAmbRed.Value,
                (byte)SliderAmbGreen.Value,
                (byte)SliderAmbBlue.Value));

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

        private void PreviewLighting_Click(object sender, RoutedEventArgs e)
        {
            var d3dColor = Color.FromArgb(
                (byte)SliderD3DAlpha.Value,
                (byte)SliderD3DRed.Value,
                (byte)SliderD3DGreen.Value,
                (byte)SliderD3DBlue.Value);

            var specColor = Color.FromArgb(
                (byte)SliderSpecAlpha.Value,
                (byte)SliderSpecRed.Value,
                (byte)SliderSpecGreen.Value,
                (byte)SliderSpecBlue.Value);

            var ambColor = Color.FromRgb(
                (byte)SliderAmbRed.Value,
                (byte)SliderAmbGreen.Value,
                (byte)SliderAmbBlue.Value);

            var dlg = new Dialogs.LightingPreviewDialog(d3dColor, specColor, ambColor)
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }

        private void ApplyChanges()
        {
            if (!LightsDataService.Instance.IsLoaded) return;

            try
            {
                Debug.WriteLine("[LightPropertiesPanel.ApplyChanges] Saving properties...");

                // Build packed colors
                uint d3dColor = ((uint)(byte)SliderD3DAlpha.Value << 24) |
                                ((uint)(byte)SliderD3DRed.Value << 16) |
                                ((uint)(byte)SliderD3DGreen.Value << 8) |
                                (byte)SliderD3DBlue.Value;

                uint specColor = ((uint)(byte)SliderSpecAlpha.Value << 24) |
                                 ((uint)(byte)SliderSpecRed.Value << 16) |
                                 ((uint)(byte)SliderSpecGreen.Value << 8) |
                                 (byte)SliderSpecBlue.Value;

                // DAYTIME bit (bit 2): SET = daytime, CLEAR = night.
                // Night checkbox checked = night = leave DAYTIME bit clear.
                // Night checkbox unchecked = daytime = set DAYTIME bit.
                uint nightFlag = 0;

                if (ChkNightEnabled.IsChecked != true)
                    nightFlag |= LightsAccessor.NIGHT_FLAG_DAYTIME;

                if (ChkLampsOn.IsChecked == true)
                    nightFlag |= LightsAccessor.NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS;

                if (ChkDarkenWalls.IsChecked == true)
                    nightFlag |= LightsAccessor.NIGHT_FLAG_DARKEN_BUILDING_POINTS;

                Debug.WriteLine($"[LightPropertiesPanel.ApplyChanges] NightFlag=0x{nightFlag:X2} (Night={ChkNightEnabled.IsChecked}, Lamps={ChkLampsOn.IsChecked}, Darken={ChkDarkenWalls.IsChecked})");

                var newProps = new LightProperties
                {
                    EdLightFree = _props.EdLightFree,
                    NightFlag = nightFlag,
                    NightAmbD3DColour = d3dColor,
                    NightAmbD3DSpecular = specColor,
                    NightAmbRed   = (int)SliderAmbRed.Value   * 80 / 256,
                    NightAmbGreen = (int)SliderAmbGreen.Value * 80 / 256,
                    NightAmbBlue  = (int)SliderAmbBlue.Value  * 80 / 256,
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

                _props = newProps;
                _nightColour = newNightColour;

                Debug.WriteLine("[LightPropertiesPanel.ApplyChanges] Done!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LightPropertiesPanel.ApplyChanges] ERROR: {ex.Message}");
                MessageBox.Show($"Failed to apply properties:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}