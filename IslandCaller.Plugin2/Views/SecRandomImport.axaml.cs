using Avalonia.Controls;

namespace IslandCaller.Views;

public partial class SecRandomImport : Window
{
    public SecRandomImport()
    {
        InitializeComponent();
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var result = (isGender.IsChecked == true, male_input.Text?.ToString() ?? "", female_input.Text?.ToString() ?? "");
        Close(result);
    }
}