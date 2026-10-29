using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;

/// <summary>
/// QA / marketing tooling (2026-10-29): joins a take's numbered PNG frames (HNLTrailerTake output) and, when present,
/// its 16-bit PCM WAV into an H.264 MP4 with Unity's own MediaEncoder, so a capture can become a video on a machine
/// without ffmpeg. Frames are encoded as recorded (no scaling, no added effects). Editor only.
/// Usage: QAFramesToVideo.Encode("Marketing/Take/frames", "Marketing/Take/audio.wav", "Marketing/Take/take.mp4", 60)
/// </summary>
public static class QAFramesToVideo
{
    public static string Encode(string frameDir, string wavPath, string mp4Path, int fps, int firstFrame = 0, int lastFrame = int.MaxValue)
    {
        string[] all = Directory.GetFiles(frameDir, "*.png").OrderBy(f => f).ToArray();
        string[] frames = all.Skip(Mathf.Max(0, firstFrame)).Take(Mathf.Max(0, Mathf.Min(lastFrame, all.Length - 1) - firstFrame + 1)).ToArray(); // a cut of the take, frames as recorded
        if (frames.Length == 0) return "no PNG frames in " + frameDir;
        var first = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        first.LoadImage(File.ReadAllBytes(frames[0]));
        int w = first.width, h = first.height;
        var video = new VideoTrackAttributes { frameRate = new MediaRational(fps), width = (uint)w, height = (uint)h, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.High };

        short[] samples = null;
        int channels = 0, rate = 0;
        if (!string.IsNullOrEmpty(wavPath) && File.Exists(wavPath)) samples = ReadWav(wavPath, out channels, out rate);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(mp4Path)));
        int framesDone = 0;
        if (samples != null)
        {
            var audio = new AudioTrackAttributes { sampleRate = new MediaRational(rate), channelCount = (ushort)channels, language = "en" };
            using (var enc = new MediaEncoder(mp4Path, video, audio))
            using (var buffer = new NativeArray<float>(rate * channels / fps + channels, Allocator.Persistent))
            {
                int perFrame = rate / fps, pos = 0;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var rgba = new Texture2D(w, h, TextureFormat.RGBA32, false);
                foreach (string f in frames)
                {
                    enc.AddFrame(Rgba(f, tex, rgba));
                    framesDone++;
                    int n = Mathf.Min(perFrame * channels, samples.Length - pos);
                    if (n > 0)
                    {
                        var slice = new NativeArray<float>(n, Allocator.Temp);
                        for (int i = 0; i < n; i++) slice[i] = samples[pos + i] / 32768f;
                        enc.AddSamples(slice);
                        slice.Dispose();
                        pos += n;
                    }
                }
                Object.DestroyImmediate(tex);
            }
        }
        else
        {
            using (var enc = new MediaEncoder(mp4Path, video))
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var rgba = new Texture2D(w, h, TextureFormat.RGBA32, false);
                foreach (string f in frames) { enc.AddFrame(Rgba(f, tex, rgba)); framesDone++; }
                Object.DestroyImmediate(tex);
            }
        }
        Object.DestroyImmediate(first);
        var info = new FileInfo(mp4Path);
        return $"{mp4Path}: {framesDone} frames {w}x{h} at {fps} fps ({framesDone / (float)fps:0.00} s){(samples != null ? $", audio {channels} ch {rate} Hz" : ", no audio")}, {info.Length / 1024} KB";
    }

    // MediaEncoder takes RGBA32 only; a PNG loads as ARGB32, so its pixels are copied across
    private static Texture2D Rgba(string file, Texture2D load, Texture2D rgba)
    {
        load.LoadImage(File.ReadAllBytes(file));
        rgba.SetPixels32(load.GetPixels32());
        rgba.Apply(false);
        return rgba;
    }

    // a plain 16-bit PCM RIFF/WAVE reader
    private static short[] ReadWav(string path, out int channels, out int rate)
    {
        channels = 0; rate = 0;
        using (var r = new BinaryReader(File.OpenRead(path)))
        {
            if (new string(r.ReadChars(4)) != "RIFF") return null;
            r.ReadInt32();
            if (new string(r.ReadChars(4)) != "WAVE") return null;
            int bits = 16;
            while (r.BaseStream.Position < r.BaseStream.Length - 8)
            {
                string id = new string(r.ReadChars(4));
                int size = r.ReadInt32();
                if (id == "fmt ")
                {
                    r.ReadInt16();
                    channels = r.ReadInt16();
                    rate = r.ReadInt32();
                    r.ReadInt32(); r.ReadInt16();
                    bits = r.ReadInt16();
                    if (size > 16) r.ReadBytes(size - 16);
                }
                else if (id == "data")
                {
                    if (bits != 16) return null;
                    var s = new short[size / 2];
                    for (int i = 0; i < s.Length; i++) s[i] = r.ReadInt16();
                    return s;
                }
                else r.ReadBytes(size);
            }
        }
        return null;
    }
}
