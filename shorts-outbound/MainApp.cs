using Godot;
using System;
using System.Threading.Tasks;

public partial class MainApp : Node2D
{
	// Recording & Viewport
	private RecordingViewport _recorder;
	
	// Sidebar Controls
	private ItemList _itemListGames;
	private Slider _genSlider;
	private LineEdit _lineEditPath;
	private Button _buttonOpenPath;
	private Button _buttonRecord;
	private Label _labelGenSlider;

	// State tracking
	private bool _isRecording = false;
	private bool _cooldownActive = false;
	private string _currentGameTitle = "";

	public override void _Ready()
	{
		// Get references 
		_recorder = GetNode<RecordingViewport>("%RecordingViewport");
		_itemListGames = GetNode<ItemList>("%ItemListGames");
		_genSlider = GetNode<Slider>("%GenSlider");
		_lineEditPath = GetNode<LineEdit>("%LineEditPath");
		_buttonOpenPath = GetNode<Button>("%ButtonOpenPath");
		_buttonRecord = GetNode<Button>("%ButtonRecord");
		_labelGenSlider = GetNode<Label>("%LabelGenSlider");

		// Connect Signals to Methods
		_buttonRecord.Pressed += OnRecordButtonPressed;
		_buttonOpenPath.Pressed += OnOpenFolderPressed;
		_genSlider.ValueChanged += OnSliderValueChanged;
		_itemListGames.ItemActivated += OnGameItemActivated;
		
		_lineEditPath.Text = OS.GetUserDataDir();
		
		if (_itemListGames.ItemCount == 0) 
		{
			_itemListGames.AddItem("No Game"); 
		}
		_currentGameTitle = _itemListGames.GetItemText(0);
		
	}
	
	// --- INTERACTION METHODS ---

	private void OnSliderValueChanged(double value)
	{
		_labelGenSlider.Text = "Up to Pokémon generation: " + value.ToString();
		
	}

	private void OnGameItemActivated(long index)
	{
		// ItemActivated triggers on double-click or Enter
		string gameName = _itemListGames.GetItemText((int)index);
		GD.Print($"Loading Game: {gameName}");
		
		// This is where you'll eventually call your LoadGame logic
		// LoadGameByName(gameName);
	}

	private void OnOpenFolderPressed()
	{
		string path = _lineEditPath.Text;
		
		// OS.ShellOpen opens the path in the system's file explorer (Windows/Mac/Linux)
		// We use GlobalizePath to ensure it's a real system path, not a "user://" path
		string absolutePath = ProjectSettings.GlobalizePath(path);
		
		Error err = OS.ShellOpen(absolutePath);
		
		if (err != Error.Ok)
		{
			GD.PrintErr($"Could not open folder: {absolutePath}");
		}
	}

	private async void OnRecordButtonPressed()
	{
		if (_cooldownActive) return;

		if (!_isRecording)
		{
			StartRecording();
		}
		else
		{
			StopRecording();
		}

		await StartButtonCooldown(1.0f);
	}

	private void StartRecording()
	{
		string timestamp = Time.GetDatetimeStringFromSystem().Replace(":", "-");
		// Combine path from LineEdit with the filename
		
		string path = _lineEditPath.Text.PathJoin(_currentGameTitle + $"{timestamp}.avi");

		_recorder.StartRecording(path);

		_isRecording = true;
		_buttonRecord.Text = "STOP RECORDING";
		_buttonRecord.Modulate = new Color(1, 0, 0);
	}

	private void StopRecording()
	{
		_recorder.StopRecording();

		_isRecording = false;
		_buttonRecord.Text = "START RECORDING";
		_buttonRecord.Modulate = new Color(1, 1, 1);
	}

	private async Task StartButtonCooldown(float seconds)
	{
		_cooldownActive = true;
		_buttonRecord.Disabled = true;
		
		await Task.Delay((int)(seconds * 1000));

		_buttonRecord.Disabled = false;
		_cooldownActive = false;
	}
}
