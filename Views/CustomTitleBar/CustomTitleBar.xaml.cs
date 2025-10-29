using System.Windows;

namespace MusicPlayer.Views.CustomTitleBar;

public partial class CustomTitleBar
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(CustomTitleBar), 
            new PropertyMetadata("Music Player"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public CustomTitleBar()
    {
        InitializeComponent();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)!.WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null) { return; }
            
        if (window.WindowState == WindowState.Maximized)
        {
            window.WindowState = WindowState.Normal;
            BtnMaximize.Content = "â–¡";
        }
        else
        {
            window.WindowState = WindowState.Maximized;
            BtnMaximize.Content = "â";
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            BtnMaximize_Click(sender, e);
        }
        else
        {
            Window.GetWindow(this)?.DragMove();
        }
    }
}