using System.Text;

namespace Timer.Core;

/// <summary>Creates shared audio for Windows and Android.</summary>
public static class TimerSoundAudio
{
    /// <summary>Creates a mono 16-bit 22050 Hz WAV for the requested cue.</summary>
    public static byte[] CreateWave(TimerSound sound)
    {
        const int rate = 22050;
        var gong = sound == TimerSound.Gong;
        var count = sound == TimerSound.Rest ? 3 : sound == TimerSound.Work ? 2 : 1;
        var duration = gong ? 2.4 : count * 0.22;
        var samples = (int)(rate * duration);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples * 2);

        for (var index = 0; index < samples; index++)
        {
            var time = index / (double)rate;
            double value;
            if (gong)
            {
                var envelope = Math.Min(1, time / 0.008) * Math.Min(1, (duration - time) / 0.08);
                value = envelope * (Math.Sin(2 * Math.PI * 180 * time) * Math.Exp(-time * 1.7) +
                    0.5 * Math.Sin(2 * Math.PI * 367 * time) * Math.Exp(-time * 2.2) +
                    0.3 * Math.Sin(2 * Math.PI * 523 * time) * Math.Exp(-time * 3) +
                    0.2 * Math.Sin(2 * Math.PI * 811 * time) * Math.Exp(-time * 4)) / 2;
            }
            else
            {
                var pulse = time % 0.22;
                var envelope = Math.Clamp(Math.Min(pulse / 0.005, (0.12 - pulse) / 0.01), 0, 1);
                value = Math.Sin(2 * Math.PI * 880 * time) * envelope * 0.5;
            }

            writer.Write((short)(value * short.MaxValue));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
