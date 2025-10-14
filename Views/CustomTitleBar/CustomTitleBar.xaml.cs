using System.Windows;

namespace MusicPlayer.Views.CustomTitleBar
{
    public partial class CustomTitleBar
    {
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
                BtnMaximize.Content = "□";
            }
            else
            {
                window.WindowState = WindowState.Maximized;
                BtnMaximize.Content = "❐";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

