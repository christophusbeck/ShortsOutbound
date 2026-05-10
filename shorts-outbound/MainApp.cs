using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

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
	private Button _buttonResetGame;
	private CheckBox _checkBoxFacecam;
	private TextureRect _facecam;
	
	// Music UI References
	private CheckBox _checkBoxMusic;
	private ItemList _itemListMusic;
	private LineEdit _lineEditMusicFolder;
	private Button _buttonMusicFolder;

	// State tracking
	private bool _isRecording = false;
	private bool _cooldownActive = false;
	private string _currentGameTitle = "";
	private Node _currentGame; // Tracks the currently active minigame
	private Dictionary<string, string> _gameLibrary = new();

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
		_buttonResetGame = GetNode<Button>("%ButtonResetGame");
		_checkBoxFacecam = GetNode<CheckBox>("%CheckBoxFacecam");
   		_facecam = GetNode<TextureRect>("%Facecam");
		
		// Music References (using the unique names from your image)
		_checkBoxMusic = GetNode<CheckBox>("%CheckBoxMusic");
		_itemListMusic = GetNode<ItemList>("%ItemListMusic");
		_lineEditMusicFolder = GetNode<LineEdit>("%LineEditMusicFolder");
		_buttonMusicFolder = GetNode<Button>("%ButtonMusicFolder");

		// Connect Signals to Methods
		_buttonRecord.Pressed += OnRecordButtonPressed;
		_buttonOpenPath.Pressed += OnOpenFolderPressed;
		_genSlider.ValueChanged += OnSliderValueChanged;
		_itemListGames.ItemActivated += OnGameItemActivated;
		_buttonResetGame.Pressed += OnResetButtonPressed;
		_checkBoxFacecam.Toggled += OnFacecamToggled;
		_buttonMusicFolder.Pressed += OnOpenMusicFolderPressed;
		
		_checkBoxFacecam.ButtonPressed = true;
		_facecam.Visible = true;
		_lineEditPath.Text = OS.GetUserDataDir();
		
		SetupMusicSystem();
		ScanGamesFolder();
		
		if (_itemListGames.ItemCount == 0) 
		{
			_itemListGames.AddItem("No Game"); 
		}
		_currentGameTitle = _itemListGames.GetItemText(0);
		
	}
	private void ScanGamesFolder()
	{
		_gameLibrary.Clear();
		_itemListGames.Clear();

		string path = "res://Games/";
		using var dir = DirAccess.Open(path);

		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();

			while (fileName != "")
			{
				if (!dir.CurrentIsDir())
				{
					// Check for both the source and the exported 'remap' version
					if (fileName.EndsWith(".tscn") || fileName.EndsWith(".tscn.remap"))
					{
						// Remove extensions to get the clean name (e.g., "StatJack")
						string gameName = fileName.Replace(".tscn", "").Replace(".remap", "");
						
						// Always point the library to the standard .tscn path
						// Godot's ResourceLoader will handle the redirection internally
						string fullPath = path + gameName + ".tscn";
						
						if (!_gameLibrary.ContainsKey(gameName))
						{
							_gameLibrary.Add(gameName, fullPath);
							_itemListGames.AddItem(gameName);
							GD.Print($"Successfully indexed: {gameName}");
						}
					}
				}
				fileName = dir.GetNext();
			}
		}
		
		
	}
	
	private void SetupMusicSystem()
	{
		string internalPath = "res://Music/";
		
		// Ensure directory exists
		if (!DirAccess.DirExistsAbsolute(internalPath))
		{
			DirAccess.MakeDirAbsolute(internalPath);
		}

		// Convert res:// to C:/Users/...
		string globalPath = ProjectSettings.GlobalizePath(internalPath);
		_lineEditMusicFolder.Text = globalPath;

		_itemListMusic.Clear();
		string[] allowedExtensions = { ".mp3", ".wav", ".ogg", ".ogv", ".m4a", ".aac" };
		int validFileCount = 0;

		// IMPORTANT: Open the GLOBAL path, not the internal one
		using var dir = DirAccess.Open(globalPath); 
		
		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();

			while (fileName != "")
			{
				// Skip the .import files Godot creates automatically
				if (!dir.CurrentIsDir() && !fileName.EndsWith(".import"))
				{
					bool isValid = false;
					foreach (string ext in allowedExtensions)
					{
						if (fileName.ToLower().EndsWith(ext))
						{
							isValid = true;
							break;
						}
					}

					if (isValid)
					{
						_itemListMusic.AddItem(fileName);
						validFileCount++;
					}
				}
				fileName = dir.GetNext();
			}

			_checkBoxMusic.Disabled = (validFileCount == 0);
			GD.Print($"Scan complete. Found {validFileCount} valid files in {globalPath}");
		}
		else
		{
			GD.PrintErr($"Failed to open directory at: {globalPath}");
		}
	}
	
	// --- INTERACTION METHODS ---
	
	private void OnOpenMusicFolderPressed()
{
	// Since the SetupMusicSystem already globalized the path into the LineEdit,
	// we can use it directly.
	string absolutePath = _lineEditMusicFolder.Text;
	
	// Safety check: ensure the folder still exists before trying to open it
	if (DirAccess.DirExistsAbsolute(absolutePath))
	{
		Error err = OS.ShellOpen(absolutePath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"Could not open music folder: {absolutePath}");
		}
	}
	else
	{
		GD.PrintErr("Music folder path does not exist on disk.");
	}
}

	private void OnSliderValueChanged(double value)
	{
		int selectedGen = (int)value;
		_labelGenSlider.Text = "Up to Pokémon generation: " + selectedGen.ToString();
		
	
		// If a game is currently running, update it live
		if (_currentGame is IBaseGame gameInterface)
		{
			LoadGame(_gameLibrary[_currentGameTitle]);
			gameInterface.SetGeneration(selectedGen);
		}
	}
	
	private void OnResetButtonPressed() 
	{
		GD.Print("Resetting current game...");

		// Check if there is a game currently running
		if (_currentGame != null && _currentGame is IBaseGame game)
		{
			// 1. Stop the current game logic (clears timers, tweens, etc.)
			game.StopGame();

			// 2. Refresh the generation (in case the slider was moved)
			game.SetGeneration((int)_genSlider.Value);

			// 3. Start it fresh
			game.StartGame();
			
			GD.Print($"Game '{_currentGameTitle}' has been reset.");
		}
		else
		{
			GD.Print("No active game to reset.");
		}
	}

	private void OnGameItemActivated(long index)
	{
		_currentGameTitle = _itemListGames.GetItemText((int)index);
		
		if (_gameLibrary.ContainsKey(_currentGameTitle))
		{
			LoadGame(_gameLibrary[_currentGameTitle]);
		}
	}
	
	public void LoadGame(string scenePath)
	{
		// 1. Clean up the existing game
		if (_currentGame != null)
		{
			_currentGame.QueueFree();
			// Optional: Call _currentGame.StopGame() if using the interface
			_currentGame = null; 
		}

		// 2. Load and Instance the new game
		PackedScene gameScene = GD.Load<PackedScene>(scenePath);
		if (gameScene == null) return;

		_currentGame = gameScene.Instantiate();
		
		// 3. Add to the Recording Zone
		GetNode<Node2D>("%GameContainer").AddChild(_currentGame);

		// 4. Initialize if it follows our IBaseGame interface
		if (_currentGame is IBaseGame game)
		{
			// Set the current slider value immediately before starting
			game.SetGeneration((int)_genSlider.Value);
			game.StartGame();
		}
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
	
	private void OnFacecamToggled(bool isToggled)
	{
		_facecam.Visible = isToggled;
		GD.Print("Facecam " + (isToggled ? "Enabled" : "Disabled"));
	}
}
