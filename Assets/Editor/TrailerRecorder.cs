using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// TRAILER: records the Game view to an H.264 MP4 (with game audio) from code, for scripted gameplay takes in
/// Play mode. Start(path) begins a take (path without extension), Stop() ends it. The Recorder plays the game at a
/// constant frame rate while it records, so takes stay smooth even if the editor is slow.
/// </summary>
public static class TrailerRecorder
{
    private static RecorderController controller;

    public static bool IsRecording => controller != null && controller.IsRecording();

    public static void Start(string outputPathNoExtension, int width = 1920, int height = 1080, float fps = 60f)
    {
        Stop();
        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Trailer Take";
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        movie.CaptureAudio = true;
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
        movie.OutputFile = outputPathNoExtension;
        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToManual();
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.FrameRate = fps;
        settings.CapFrameRate = true;
        controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording();
    }

    public static void Stop()
    {
        if (controller == null) return;
        if (controller.IsRecording()) controller.StopRecording();
        controller = null;
    }
}
