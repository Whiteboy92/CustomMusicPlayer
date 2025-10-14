    using NAudio.Dsp;
    using NAudio.Wave;

    namespace MusicPlayer.Services;

    public class EqualizerService : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int channels;
        private readonly BiQuadFilter[,] filters;

        public float Band80Hz { get; set; }
        public float Band240Hz { get; set; }
        public float Band750Hz { get; set; }
        public float Band2200Hz { get; set; }
        public float Band6600Hz { get; set; }

        public WaveFormat WaveFormat => source.WaveFormat;

        public EqualizerService(ISampleProvider source)
        {
            this.source = source;
            channels = source.WaveFormat.Channels;
            filters = new BiQuadFilter[channels, 5];
            
            InitializeFilters();
        }

        private void InitializeFilters()
        {
            float sampleRate = source.WaveFormat.SampleRate;
            
            for (int ch = 0; ch < channels; ch++)
            {
                filters[ch, 0] = BiQuadFilter.PeakingEQ(sampleRate, 80, 1.2f, 0);
                filters[ch, 1] = BiQuadFilter.PeakingEQ(sampleRate, 240, 1.2f, 0);
                filters[ch, 2] = BiQuadFilter.PeakingEQ(sampleRate, 750, 1.0f, 0);
                filters[ch, 3] = BiQuadFilter.PeakingEQ(sampleRate, 2200, 1.0f, 0);
                filters[ch, 4] = BiQuadFilter.PeakingEQ(sampleRate, 6600, 1.2f, 0);
            }
        }

        private void UpdateFilters()
        {
            float sampleRate = source.WaveFormat.SampleRate;
            
            for (int ch = 0; ch < channels; ch++)
            {
                filters[ch, 0] = BiQuadFilter.PeakingEQ(sampleRate, 80, 1.2f, Band80Hz);
                filters[ch, 1] = BiQuadFilter.PeakingEQ(sampleRate, 240, 1.2f, Band240Hz);
                filters[ch, 2] = BiQuadFilter.PeakingEQ(sampleRate, 750, 1.0f, Band750Hz);
                filters[ch, 3] = BiQuadFilter.PeakingEQ(sampleRate, 2200, 1.0f, Band2200Hz);
                filters[ch, 4] = BiQuadFilter.PeakingEQ(sampleRate, 6600, 1.2f, Band6600Hz);
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            UpdateFilters();
            for (int i = 0; i < samplesRead; i++)
            {
                int channel = i % channels;
                float sample = buffer[offset + i];
                sample = filters[channel, 0].Transform(sample);
                sample = filters[channel, 1].Transform(sample); 
                sample = filters[channel, 2].Transform(sample);
                sample = filters[channel, 3].Transform(sample);
                sample = filters[channel, 4].Transform(sample);

                buffer[offset + i] = sample;
            }

            return samplesRead;
        }
    }