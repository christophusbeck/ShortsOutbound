using Godot;
using System;
using System.Threading.Tasks; // Added for Task.Delay

public partial class MainApp : Node2D
{
	private Button _recordButton;
	private Node _recorder;
	
	private bool _isRecording = false;
	private bool _cooldownActive = false; // Prevents spamming

	public override void _Ready()
	{
		_recordButton = GetNode<Button>("%ButtonRecord");
		_recorder = GetNode<Node>("%RecordingViewport");

		_recordButton.Pressed += OnRecordButtonPressed;
	}

	private async void OnRecordButtonPressed()
	{
		// If we are in the middle of a cooldown, ignore the click entirely
		if (_cooldownActive) return;

		if (!_isRecording)
		{
			StartRecording();
		}
		else
		{
			StopRecording();
		}

		// Trigger the cooldown
		await StartButtonCooldown(1.0f); // 1 second lockout
	}

	private async Task StartButtonCooldown(float seconds)
	{
		_cooldownActive = true;
		
		// Visual feedback: Make the button slightly transparent to show it's disabled
		_recordButton.Disabled = true; 
		float originalAlpha = _recordButton.Modulate.A;
		_recordButton.Modulate = new Color(_recordButton.Modulate.R, _recordButton.Modulate.G, _recordButton.Modulate.B, 0.5f);

		// Wait for the specified time
		await Task.Delay((int)(seconds * 1000));

		// Restore button
		_recordButton.Disabled = false;
		_recordButton.Modulate = new Color(_recordButton.Modulate.R, _recordButton.Modulate.G, _recordButton.Modulate.B, originalAlpha);
		_cooldownActive = false;
	}

	private void StartRecording()
	{
		string timestamp = Time.GetDatetimeStringFromSystem().Replace(":", "-");
		string fileName = $"user://TikTok_{timestamp}.avi"; // Changed to user:// for write permissions

		_recorder.Call("StartRecording", fileName);

		_isRecording = true;
		_recordButton.Text = "STOP RECORDING";
		_recordButton.Modulate = new Color(1, 0, 0); // Red
	}

	private void StopRecording()
	{
		_recorder.Call("StopRecording");

		_isRecording = false;
		_recordButton.Text = "START RECORDING";
		_recordButton.Modulate = new Color(1, 1, 1); // White
	}
}
