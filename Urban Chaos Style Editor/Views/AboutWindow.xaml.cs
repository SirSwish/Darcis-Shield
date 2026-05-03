using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UrbanChaosEditor.Shared.Constants;

namespace UrbanChaosStyleEditor.Views
{
    public partial class AboutWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = WindowsInteropConstants.DWMWA_USE_IMMERSIVE_DARK_MODE;
        private const int DWMWA_CAPTION_COLOR = WindowsInteropConstants.DWMWA_CAPTION_COLOR;
        private const int DWMWA_TEXT_COLOR = WindowsInteropConstants.DWMWA_TEXT_COLOR;

        public AboutWindow()
        {
            InitializeComponent();
            SourceInitialized += OnSourceInitialized;

            var asm = Assembly.GetExecutingAssembly();
            VersionText.Text =
                asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "Unknown";
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                int dark = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                int caption = 0x001E1E1E;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));

                int text = 0x00FFFFFF;
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            }
            catch { }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
