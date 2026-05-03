using Godot;
using System;
using OpenCvSharp;
using OpenCvSharp.Dnn;

public partial class FaceCam : TextureRect
{
	private VideoCapture _capture;
	private ImageTexture _texture;
	private Net _segmentationNet;
	
	private readonly Size _modelSize = new Size(256, 256);

	public override void _Ready()
	{
		_capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
		_texture = new ImageTexture();

		string modelPath = ProjectSettings.GlobalizePath("res://models/model.onnx");
		try 
		{
			_segmentationNet = CvDnn.ReadNetFromOnnx(modelPath);
			_segmentationNet.SetPreferableBackend(Backend.OPENCV);
			_segmentationNet.SetPreferableTarget(Target.CPU);
			GD.Print("AI Model loaded successfully!");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to load ONNX model: {e.Message}");
		}

		if (!_capture.IsOpened())
		{
			GD.PrintErr("Webcam konnte nicht geöffnet werden!");
		}
	}

	public override void _Process(double delta)
	{
		if (_capture == null || !_capture.IsOpened()) return;

		using (Mat rawFrame = new Mat())
		{
			_capture.Read(rawFrame);
			if (rawFrame.Empty()) return;

			// --- 1. GET TARGET SIZE FROM THE TEXTURERECT ---
			// We convert Godot's Vector2 to OpenCV's Size
			Size targetSize = new Size((int)Size.X, (int)Size.Y);

			// --- 2. AI MASK GENERATION ---
			using Mat mask = GenerateMask(rawFrame, targetSize);

			// --- 3. RESIZE FRAME & COLOR CORRECTION ---
			using Mat resizedFrame = new Mat();
			Cv2.Resize(rawFrame, resizedFrame, targetSize);
			Cv2.CvtColor(resizedFrame, resizedFrame, ColorConversionCodes.BGR2RGBA);
			
			// Inject the AI mask into the Alpha channel
			ApplyMaskToAlpha(resizedFrame, mask);

			// --- 4. CONVERT TO GODOT ---
			byte[] rawData = new byte[resizedFrame.Total() * resizedFrame.ElemSize()];
			System.Runtime.InteropServices.Marshal.Copy(resizedFrame.Data, rawData, 0, rawData.Length);

			var img = Godot.Image.CreateFromData(
				resizedFrame.Cols, 
				resizedFrame.Rows, 
				false, 
				Godot.Image.Format.Rgba8, 
				rawData
			);

			if (img != null)
			{
				_texture.SetImage(img);
				this.Texture = _texture;
			}
		}
	}

	private Mat GenerateMask(Mat input, Size targetSize)
	{
		using Mat blob = CvDnn.BlobFromImage(input, 1.0 / 255.0, _modelSize, new Scalar(0, 0, 0), true, false);
		_segmentationNet.SetInput(blob);

		using Mat output = _segmentationNet.Forward();
		
		using Mat maskSmall = Mat.FromPixelData(_modelSize.Height, _modelSize.Width, MatType.CV_32FC1, output.Ptr(0));
		
		using Mat thresholded = new Mat();
		Cv2.Threshold(maskSmall, thresholded, 0.5, 255, ThresholdTypes.Binary);
		thresholded.ConvertTo(thresholded, MatType.CV_8UC1);

		// Resize mask directly to match the TextureRect size
		Mat fullMask = new Mat();
		Cv2.Resize(thresholded, fullMask, targetSize);
		return fullMask;
	}

	private void ApplyMaskToAlpha(Mat rgbaFrame, Mat mask)
	{
		Mat[] channels = Cv2.Split(rgbaFrame);
		channels[3] = mask; 
		Cv2.Merge(channels, rgbaFrame);
		
		foreach(var c in channels) c.Dispose();
	}

	public override void _ExitTree()
	{
		if (_capture != null)
		{
			_capture.Release();
			_capture.Dispose();
		}
		_segmentationNet?.Dispose();
	}
}
