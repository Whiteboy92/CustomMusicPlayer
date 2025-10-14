using System.Windows;

namespace MusicPlayer.Views.Equalizer
{
    public partial class EqualizerView
    {
        public event EventHandler<(float band80, float band240, float band750, float band2200, float band6600)>? EqualizerChanged;

        public EqualizerView()
        {
            InitializeComponent();
        }

        public void SetEqualizerValues(float band80, float band240, float band750, float band2200, float band6600)
        {
            Band80Slider.Value = band80;
            Band240Slider.Value = band240;
            Band750Slider.Value = band750;
            Band2200Slider.Value = band2200;
            Band6600Slider.Value = band6600;

            Band80Value.Text = $"{band80:+0.0;-0.0;0.0} dB";
            Band240Value.Text = $"{band240:+0.0;-0.0;0.0} dB";
            Band750Value.Text = $"{band750:+0.0;-0.0;0.0} dB";
            Band2200Value.Text = $"{band2200:+0.0;-0.0;0.0} dB";
            Band6600Value.Text = $"{band6600:+0.0;-0.0;0.0} dB";
        }

        private void Band80Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Band80Value == null) return;
            
            float value = (float)Band80Slider.Value;
            Band80Value.Text = $"{value:+0.0;-0.0;0.0} dB";
            
            NotifyEqualizerChanged();
        }

        private void Band240Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Band240Value == null) return;
            
            float value = (float)Band240Slider.Value;
            Band240Value.Text = $"{value:+0.0;-0.0;0.0} dB";
            
            NotifyEqualizerChanged();
        }

        private void Band750Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Band750Value == null) return;
            
            float value = (float)Band750Slider.Value;
            Band750Value.Text = $"{value:+0.0;-0.0;0.0} dB";
            
            NotifyEqualizerChanged();
        }

        private void Band2200Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Band2200Value == null) return;
            
            float value = (float)Band2200Slider.Value;
            Band2200Value.Text = $"{value:+0.0;-0.0;0.0} dB";
            
            NotifyEqualizerChanged();
        }

        private void Band6600Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Band6600Value == null) return;
            
            float value = (float)Band6600Slider.Value;
            Band6600Value.Text = $"{value:+0.0;-0.0;0.0} dB";
            
            NotifyEqualizerChanged();
        }

        private void NotifyEqualizerChanged()
        {
            float band80 = (float)Band80Slider.Value;
            float band240 = (float)Band240Slider.Value;
            float band750 = (float)Band750Slider.Value;
            float band2200 = (float)Band2200Slider.Value;
            float band6600 = (float)Band6600Slider.Value;
            
            EqualizerChanged?.Invoke(this, (band80, band240, band750, band2200, band6600));
        }
    }
}
