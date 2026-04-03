using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using UrbanChaosLightEditor.Services;
using MediaColor = System.Windows.Media.Color;

namespace UrbanChaosLightEditor.Views.Dialogs
{
    public partial class LightingPreviewDialog : Window
    {
        private readonly MediaColor _d3dColor;
        private readonly MediaColor _specularColor;
        private readonly MediaColor _nightAmbient;

        public LightingPreviewDialog(MediaColor d3dColor, MediaColor specularColor, MediaColor nightAmbient)
        {
            InitializeComponent();
            _d3dColor = d3dColor;
            _specularColor = specularColor;
            _nightAmbient = nightAmbient;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("pack://application:,,,/Assets/Images/Preview-1.PNG");
            ImgOriginal.Source = new BitmapImage(uri);

            var streamInfo = Application.GetResourceStream(uri)!;
            byte[] imageBytes;

            using (var ms = new MemoryStream())
            {
                streamInfo.Stream.CopyTo(ms);
                imageBytes = ms.ToArray();
            }

            // For this preview mode, use the ambient swatch colour directly
            // as the image filter colour.
            var filterColor = _nightAmbient;

            try
            {
                var litSource = await Task.Run(() =>
                {
                    using var srcStream = new MemoryStream(imageBytes);
                    using var srcBitmap = new System.Drawing.Bitmap(srcStream);
                    using var litBitmap = LightPreviewRenderer.ApplyPreviewLighting(
                        srcBitmap,
                        filterColor,
                        filterStrength: 1.0f);

                    using var outStream = new MemoryStream();
                    litBitmap.Save(outStream, System.Drawing.Imaging.ImageFormat.Png);
                    outStream.Position = 0;

                    var bitmapSource = BitmapFrame.Create(
                        outStream,
                        BitmapCreateOptions.None,
                        BitmapCacheOption.OnLoad);

                    bitmapSource.Freeze();
                    return bitmapSource;
                });

                ImgLit.Source = litSource;
                ImgLit.Visibility = Visibility.Visible;
                TxtStatus.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Render failed: {ex.Message}";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}