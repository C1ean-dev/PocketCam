using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PocketCam.Desktop;

namespace PocketCam.Desktop.Tests;

public sealed class ThemeTests
{
    [Fact]
    public void CameraSelectorsUseDarkBackgroundAndReadableText()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();

                var comboStyle = Assert.IsType<Style>(app.Resources[typeof(ComboBox)]);
                var combo = new ComboBox
                {
                    Style = comboStyle,
                    Width = 240,
                    ItemsSource = new[] { "1280 × 720", "1920 × 1080" },
                    SelectedIndex = 0,
                };

                combo.ApplyTemplate();

                AssertBrushColor(combo.Background, Color.FromRgb(0x17, 0x38, 0x3C));
                AssertBrushColor(combo.Foreground, Colors.White);

                var selectionBorder = Assert.IsType<Border>(combo.Template.FindName("SelectionBorder", combo));
                AssertBrushColor(selectionBorder.Background, Color.FromRgb(0x17, 0x38, 0x3C));

                var popup = Assert.IsType<Popup>(combo.Template.FindName("PART_Popup", combo));
                var popupBorder = Assert.IsType<Border>(popup.Child);
                AssertBrushColor(popupBorder.Background, Color.FromRgb(0x17, 0x38, 0x3C));

                var itemStyle = Assert.IsType<Style>(app.Resources[typeof(ComboBoxItem)]);
                var item = new ComboBoxItem { Style = itemStyle, Content = "Traseira" };
                AssertBrushColor(item.Background, Color.FromRgb(0x17, 0x38, 0x3C));
                AssertBrushColor(item.Foreground, Colors.White);
            }
            catch (Exception error)
            {
                failure = error;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    private static void AssertBrushColor(Brush brush, Color expected)
    {
        var solid = Assert.IsType<SolidColorBrush>(brush);
        Assert.Equal(expected, solid.Color);
    }
}
