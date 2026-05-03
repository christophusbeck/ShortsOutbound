using Godot;
using System;
using OpenCvSharp;

public partial class RecordingViewport : SubViewport
{
	[Export] public int FPS = 30;
	// Changed to .avi for MJPG stability
	//[Export] public string FileName = "user://minigame_recording.avi";
	
	private VideoWriter _writer;
	private bool _isRecording = false;
	private Vector2I _size;
	private double _timeSinceLastFrame = 0.0;
	private double _targetFrameTime = 0.0;

	public override void _Ready()
	{
		// No need for TargetViewport = this; if the script is ON the SubViewport
	}



	public void StartRecording(string FileName)
	{
		_size = GetSize(); 
		// Ensure we know exactly how long a frame should last (e.g., 1/30 = 0.0333s)
		_targetFrameTime = 1.0 / FPS;
		_timeSinceLastFrame = 0.0;

		int fourcc = VideoWriter.FourCC('M', 'J', 'P', 'G');
		string absolutePath = ProjectSettings.GlobalizePath(FileName);

		_writer = new VideoWriter(absolutePath, fourcc, FPS, new Size(_size.X, _size.Y));

		if (!_writer.IsOpened()) {
			GD.PrintErr("VIDEO WRITER ERROR: Could not open file.");
			return;
		}

		_isRecording = true;
		GD.Print($"Recording started: {FPS} FPS at {_size.X}x{_size.Y}");
		GD.Print("FileName: " + absolutePath);
	}

	public override void _Process(double delta)
	{
		if (!_isRecording || _writer == null) return;

		_timeSinceLastFrame += delta;

		// We use a WHILE loop here. If the game lags and misses a frame, 
		// this ensures the video keeps the correct "real time" length.
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

			// --- THE FLIP LOGIC ---
			// If it was upside down with FlipMode.X, it means we don't need to flip at all, 
			// OR we need to flip it differently. 
			// TRY THIS: Change FlipMode.X to FlipMode.Y, or comment the line out entirely.
			//Cv2.Flip(bgrFrame, bgrFrame, FlipMode.X); 

			_writer.Write(bgrFrame);
		}
	}

	public void StopRecording()
	{
		if (!_isRecording) return;
		_isRecording = false;
		
		// Ensure data is flushed to disk
		_writer?.Release();
		_writer?.Dispose();
		_writer = null;
		GD.Print("Recording saved and writer released.");
	}

	public override void _ExitTree()
	{
		StopRecording();
	}
}
