using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using UrbanChaosLightEditor.Models;
using UrbanChaosLightEditor.Services;

namespace UrbanChaosLightEditor.Views.Dialogs
{
    public partial class LightPropertiesDialog : Window
    {
        private readonly LightsAccessor _acc;
        private LightProperties _props;
        private LightNightColour _nightColour;
        private bool _isLoading = true;

        public LightPropertiesDialog()
        {
            InitializeComponent();
            _acc = new LightsAccessor(LightsDataService.Instance);
            Loaded += (_, __) => LoadProperties();
        }

        private void LoadProperties()
        {
            _isLoading = true;

            try
            {
                _props = _acc.ReadProperties();
                _nightColour = _acc.ReadNightColour();

                Debug.WriteLine($"[LightPropertiesDialog.Load] D3D=0x{_props.NightAmbD3DColour:X8} => ARGB({_props.D3DAlpha},{_props.D3DRed},{_props.D3DGreen},{_props.D3DBlue})");
                Debug.WriteLine($"[LightPropertiesDialog.Load] Spec=0x{_props.NightAmbD3DSpecular:X8}");
                Debug.WriteLine($"[LightPropertiesDialog.Load] AmbRGB=({_props.NightAmbRed},{_props.NightAmbGreen},{_props.NightAmbBlue})");
                Debug.WriteLine($"[LightPropertiesDialog.Load] LampRGB=({_props.NightLampostRed},{_props.NightLampostGreen},{_props.NightLampostBlue}), Radius={_props.NightLampostRadius}");
                Debug.WriteLine($"[LightPropertiesDialog.Load] Sky=({_nightColour.Red},{_nightColour.Green},{_nightColour.Blue})");

                // D3D Color (ARGB)
                SliderD3DAlpha.Value = _props.D3DAlpha;
                SliderD3DRed.Value = _props.D3DRed;
                SliderD3DGreen.Value = _props.D3DGreen;
                SliderD3DBlue.Value = _props.D3DBlue;

                // Specular (ARGB)
                SliderSpecAlpha.Value = _props.SpecularAlpha;
                SliderSpecRed.Value = _props.SpecularRed;
                SliderSpecGreen.Value = _props.SpecularGreen;
                SliderSpecBlue.Value = _props.SpecularBlue;

                // Ambient — expand engine-scaled internal value to UI 0-255: ui = internal * 820 / 256
                SliderAmbRed.Value   = Math.Clamp(_props.NightAmbRed   * 820 / 256, 0, 255);
                SliderAmbGreen.Value = Math.Clamp(_props.NightAmbGreen * 820 / 256, 0, 255);
                SliderAmbBlue.Value  = Math.Clamp(_props.NightAmbBlue  * 820 / 256, 0, 255);

                // Lamppost RGB + Radius (lamp values are sbyte, shift to 0-255 for slider)
                SliderLampRed.Value = _props.NightLampostRed + 128;
                SliderLampGreen.Value = _props.NightLampostGreen + 128;
                SliderLampBlue.Value = _props.NightLampostBlue + 128;
                SliderLampRadius.Value = _props.NightLampostRadius;

                // Night/Sky colour
                SliderSkyRed.Value = _nightColour.Red;
                SliderSkyGreen.Value = _nightColour.Green;
                SliderSkyBlue.Value = _nightColour.Blue;

                // Night flags: bit 2 (0x04) = DAYTIME. Night = DAYTIME bit clear.
                ChkNightEnabled.IsChecked = (_props.NightFlag & LightsAccessor.NIGHT_FLAG_DAYTIME) == 0;
                ChkLampsOn.IsChecked = (_props.NightFlag & LightsAccessor.NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS) != 0;
                ChkDarkenWalls.IsChecked = (_props.NightFlag & LightsAccessor.NIGHT_FLAG_DARKEN_BUILDING_POINTS) != 0;

                UpdateAllPreviews();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LightPropertiesDialog.Load] ERROR: {ex.Message}");
                MessageBox.Show($"Failed to load light properties:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnD3DChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            UpdateD3DPreview();
        }

        private void OnAmbientChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            UpdateAmbientPreview();
        }

        private void OnLampChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            UpdateLampPreview();
        }

        private void OnSkyChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            UpdateSkyPreview();
        }

        private void UpdateAllPreviews()
        {
            UpdateD3DPreview();
            UpdateAmbientPreview();
            UpdateLampPreview();
            UpdateSkyPreview();
        }

        private void UpdateD3DPreview()
        {
            byte a = (byte)SliderD3DAlpha.Value;
            byte r = (byte)SliderD3DRed.Value;
            byte g = (byte)SliderD3DGreen.Value;
            byte b = (byte)SliderD3DBlue.Value;
            D3DPreview.Fill = new SolidColorBrush(Color.FromArgb(a, r, g, b));

            byte sa = (byte)SliderSpecAlpha.Value;
            byte sr = (byte)SliderSpecRed.Value;
            byte sg = (byte)SliderSpecGreen.Value;
            byte sb = (byte)SliderSpecBlue.Value;
            SpecPreview.Fill = new SolidColorBrush(Color.FromArgb(sa, sr, sg, sb));
        }

        private void UpdateAmbientPreview()
        {
            // Scale UI 0-255 to engine range (ui * 80 / 256) for accurate mood preview
            byte rb = (byte)((int)SliderAmbRed.Value   * 80 / 256);
            byte gb = (byte)((int)SliderAmbGreen.Value * 80 / 256);
            byte bb = (byte)((int)SliderAmbBlue.Value  * 80 / 256);
            AmbientPreview.Fill = new SolidColorBrush(Color.FromRgb(rb, gb, bb));
        }

        private void UpdateLampPreview()
        {
            byte r = (byte)SliderLampRed.Value;
            byte g = (byte)SliderLampGreen.Value;
            byte b = (byte)SliderLampBlue.Value;
            LampPreview.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private void UpdateSkyPreview()
        {
            byte r = (byte)SliderSkyRed.Value;
            byte g = (byte)SliderSkyGreen.Value;
            byte b = (byte)SliderSkyBlue.Value;
            SkyPreview.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("========== LightPropertiesDialog.OK_Click START ==========");

                // Read slider values
                byte d3dA = (byte)SliderD3DAlpha.Value;
                byte d3dR = (byte)SliderD3DRed.Value;
                byte d3dG = (byte)SliderD3DGreen.Value;
                byte d3dB = (byte)SliderD3DBlue.Value;

                byte specA = (byte)SliderSpecAlpha.Value;
                byte specR = (byte)SliderSpecRed.Value;
                byte specG = (byte)SliderSpecGreen.Value;
                byte specB = (byte)SliderSpecBlue.Value;

                // Compress UI 0-255 back to engine-scaled internal value: internal = ui * 80 / 256
                int ambR = (int)SliderAmbRed.Value   * 80 / 256;
                int ambG = (int)SliderAmbGreen.Value * 80 / 256;
                int ambB = (int)SliderAmbBlue.Value  * 80 / 256;

                sbyte lampR = (sbyte)((int)SliderLampRed.Value - 128);
                sbyte lampG = (sbyte)((int)SliderLampGreen.Value - 128);
                sbyte lampB = (sbyte)((int)SliderLampBlue.Value - 128);
                int lampRadius = (int)SliderLampRadius.Value;

                byte skyR = (byte)SliderSkyRed.Value;
                byte skyG = (byte)SliderSkyGreen.Value;
                byte skyB = (byte)SliderSkyBlue.Value;

                // Build packed colors
                uint d3dColor = ((uint)d3dA << 24) | ((uint)d3dR << 16) | ((uint)d3dG << 8) | d3dB;
                uint specColor = ((uint)specA << 24) | ((uint)specR << 16) | ((uint)specG << 8) | specB;

                Debug.WriteLine($"[OK_Click] D3D ARGB=({d3dA},{d3dR},{d3dG},{d3dB}) => 0x{d3dColor:X8}");
                Debug.WriteLine($"[OK_Click] Spec ARGB=({specA},{specR},{specG},{specB}) => 0x{specColor:X8}");
                Debug.WriteLine($"[OK_Click] Amb RGB=({ambR},{ambG},{ambB})");
                Debug.WriteLine($"[OK_Click] Lamp RGB=({lampR},{lampG},{lampB}), Radius={lampRadius}");
                Debug.WriteLine($"[OK_Click] Sky RGB=({skyR},{skyG},{skyB})");

                // Build night flags from checkboxes.
                // Night checked = night mode = DAYTIME bit clear. Night unchecked = daytime = DAYTIME bit set.
                uint nightFlag = 0;
                if (ChkNightEnabled.IsChecked != true)
                    nightFlag |= LightsAccessor.NIGHT_FLAG_DAYTIME;
                if (ChkLampsOn.IsChecked == true)
                    nightFlag |= LightsAccessor.NIGHT_FLAG_LIGHTS_UNDER_LAMPOSTS;
                if (ChkDarkenWalls.IsChecked == true)
                    nightFlag |= LightsAccessor.NIGHT_FLAG_DARKEN_BUILDING_POINTS;

                var newProps = new LightProperties
                {
                    EdLightFree = _props.EdLightFree,
                    NightFlag = nightFlag,
                    NightAmbD3DColour = d3dColor,
                    NightAmbD3DSpecular = specColor,
                    NightAmbRed = ambR,
                    NightAmbGreen = ambG,
                    NightAmbBlue = ambB,
                    NightLampostRed = lampR,
                    NightLampostGreen = lampG,
                    NightLampostBlue = lampB,
                    Padding = _props.Padding,
                    NightLampostRadius = lampRadius,
                };

                var newNightColour = new LightNightColour
                {
                    Red = skyR,
                    Green = skyG,
                    Blue = skyB,
                };

                Debug.WriteLine("[OK_Click] Calling WriteProperties...");
                _acc.WriteProperties(newProps);

                Debug.WriteLine($"[OK_Click] After WriteProperties: Service.Properties.D3D=0x{LightsDataService.Instance.Properties.NightAmbD3DColour:X8}");
                Debug.WriteLine($"[OK_Click] After WriteProperties: HasChanges={LightsDataService.Instance.HasChanges}");

                Debug.WriteLine("[OK_Click] Calling WriteNightColour...");
                _acc.WriteNightColour(newNightColour);

                Debug.WriteLine($"[OK_Click] After WriteNightColour: Service.NightColour=({LightsDataService.Instance.NightColour.Red},{LightsDataService.Instance.NightColour.Green},{LightsDataService.Instance.NightColour.Blue})");
                Debug.WriteLine($"[OK_Click] After WriteNightColour: HasChanges={LightsDataService.Instance.HasChanges}");

                Debug.WriteLine("========== LightPropertiesDialog.OK_Click DONE ==========");

                DialogResult = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OK_Click] ERROR: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Failed to save light properties:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}