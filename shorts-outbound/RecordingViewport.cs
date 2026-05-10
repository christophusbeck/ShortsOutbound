using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenCvSharp;

public partial class RecordingViewport : SubViewport
{
	[Export] public int FPS = 30;
	
	private VideoWriter _writer;
	private bool _isRecording = false;
	private Vector2I _size;
	private double _timeSinceLastFrame = 0.0;
	private double _targetFrameTime = 0.0;

	// New state variables for post-processing
	private string _currentVideoPath;
	private string _audioPath;
	private bool _shouldAddMusic = false;

	public override void _Ready() { }

	public void StartRecording(string fileName, bool addMusic = false, string musicPath = "")
	{
		_size = GetSize(); 
		_targetFrameTime = 1.0 / FPS;
		_timeSinceLastFrame = 0.0;

		_shouldAddMusic = addMusic;
		_audioPath = musicPath;
		_currentVideoPath = ProjectSettings.GlobalizePath(fileName);

		int fourcc = VideoWriter.FourCC('M', 'J', 'P', 'G');
		_writer = new VideoWriter(_currentVideoPath, fourcc, FPS, new Size(_size.X, _size.Y));

		if (!_writer.IsOpened()) {
			GD.PrintErr("VIDEO WRITER ERROR: Could not open file.");
			return;
		}

		_isRecording = true;
		GD.Print($"Recording started: {FPS} FPS. Music: {addMusic}");
	}

	public override void _Process(double delta)
	{
		if (!_isRecording || _writer == null) return;

		_timeSinceLastFrame += delta;

		while (_timeSinceLastFrame >= _targetFrameTime)
		{
			_timeSinceLastFrame -= _targetFrameTime;

			using Image img = GetTexture().GetImage();
			if (img == null || img.IsEmpty()) break;

			if (img.GetFormat() != Image.Format.Rgba8)
				img.Convert(Image.Format.Rgba8);

			byte[] data = img.GetData();

			using Mat rgbaFrame = Mat.FromPixelData(_size.Y, _size.X, MatType.CV_8UC4, data);
			using Mat bgrFrame = new Mat();
			
			Cv2.CvtColor(rgbaFrame, bgrFrame, ColorConversionCodes.RGBA2BGR);
			_writer.Write(bgrFrame);
		}
	}

	public void StopRecording()
	{
		if (!_isRecording) return;
		_isRecording = false;
		
		_writer?.Release();
		_writer?.Dispose();
		_writer = null;
		GD.Print("Raw video saved.");

		// Trigger post-processing if enabled
		if (_shouldAddMusic && !string.IsNullOrEmpty(_audioPath))
		{
			ApplyMusicPostProcess();
		}
	}

	private async void ApplyMusicPostProcess()
	{
		string ffmpegPath = ProjectSettings.GlobalizePath("res://Tools/ffmpeg.exe");
		string musicPathAbs = ProjectSettings.GlobalizePath(_audioPath);
		
		// We create a temporary name for the output so we don't overwrite the source while reading it
		string outputPath = _currentVideoPath.Replace(".avi", "_final.mp4");

		GD.Print("Starting FFmpeg Audio Muxing...");

		/* 
		   FFmpeg Arguments Breakdown:
		   -stream_loop -1: Loops the input audio infinitely
		   -i [audio]: The music track
		   -i [video]: The recorded MJPG video
		   -shortest: Forces the output to end when the shortest stream (the video) ends
		   -c:v libx264: Encodes to H.264 (Better for TikTok/YouTube than MJPG)
		   -pix_fmt yuv420p: Ensures compatibility with most mobile players
		*/
		string args = $"-stream_loop -1 -i \"{musicPathAbs}\" -i \"{_currentVideoPath}\" -shortest -c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 192k -y \"{outputPath}\"";

		await Task.Run(() =>
		{
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = ffmpegPath,
					Arguments = args,
					UseShellExecute = false,
					CreateNoWindow = true, // Keep it in the background
					RedirectStandardError = true
				};

				using (Process process = Process.Start(psi))
				{
					process.WaitForExit();
					GD.Print("FFmpeg Process Finished with code: " + process.ExitCode);
				}
			}
			catch (Exception e)
			{
				GD.PrintErr("FFmpeg Error: " + e.Message);
			}
		});

		GD.Print("Final Video Ready: " + outputPath);
	}

	public override void _ExitTree()
	{
		StopRecording();
	}
}
