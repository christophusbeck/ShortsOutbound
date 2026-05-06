using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class StatJack : Node2D, IBaseGame
{
	private int _currentGeneration = 1;
	private string _currentStatMode = "HP";
	
	private const int GoalScore = 500;
	private const int DealerStopThreshold = 400;

	// Node References
	private TextureRect _cardDealer, _cardPlayer, _cardBack, _cardDeck;
	private TextureRect _spriteDealer, _spritePlayer;
	private Label _labelDealerScore, _labelPlayerScore, _gameStatLabel;
	private TextureButton _buttonHit, _buttonStand;

	// Splash References
	private ColorRect _colorRectSplash;
	private TextureRect _splashTitle, _splashWin, _splashLose, _splashDraw;

	private int _playerTotal = 0;
	private int _dealerTotal = 0;
	private bool _isAnimating = false;
	private bool _waitingForClick = false;

	public override void _Ready()
	{
		// Card Containers (The Parents)
		_cardDealer = GetNode<TextureRect>("%CardDealer");
		_cardPlayer = GetNode<TextureRect>("%CardPlayer");
		
		// Sprite Children
		_spriteDealer = GetNode<TextureRect>("%SpriteDealer");
		_spritePlayer = GetNode<TextureRect>("%SpritePlayer");
		
		// Animation & UI nodes
		_cardBack = GetNode<TextureRect>("%CardBack");
		_cardDeck = GetNode<TextureRect>("CardDeck");
		_buttonHit = GetNode<TextureButton>("%TextureButtonHit");
		_buttonStand = GetNode<TextureButton>("%TextureButtonStand");
		_labelDealerScore = GetNode<Label>("%LabelDealerScore");
		_labelPlayerScore = GetNode<Label>("%LabelPlayerScore");
		_gameStatLabel = GetNode<Label>("%GameStatLabel");

		// Splash Elements
		_colorRectSplash = GetNode<ColorRect>("%ColorRectSplash");
		_splashTitle = GetNode<TextureRect>("%SplashTitle");
		_splashWin = GetNode<TextureRect>("%SplashWin");
		_splashLose = GetNode<TextureRect>("%SplashLose");
		_splashDraw = GetNode<TextureRect>("%SplashDraw");

		// Signals
		_buttonHit.Pressed += OnHitPressed;
		_buttonStand.Pressed += OnStandPressed;
		_colorRectSplash.GuiInput += OnSplashGuiInput;

		// Ensure everything is clean on load
		_cardDealer.Visible = false;
		_cardPlayer.Visible = false;
		_cardBack.Visible = false;
		_colorRectSplash.Visible = false;
	}

	public async void StartGame()
	{
		ResetGameState();
		await ShowSplash(_splashTitle);
		PickRandomStatMode();
	}

	private void ResetGameState()
	{
		_playerTotal = 0;
		_dealerTotal = 0;
		_labelPlayerScore.Text = "0";
		_labelDealerScore.Text = "0";
		
		// Hide the parent Card nodes (hides sprites automatically)
		_cardDealer.Visible = false;
		_cardPlayer.Visible = false;
		
		SetButtonsEnabled(true);
	}

	private async Task ShowSplash(TextureRect activeSplash)
	{
		_splashTitle.Visible = false;
		_splashWin.Visible = false;
		_splashLose.Visible = false;
		_splashDraw.Visible = false;

		activeSplash.Visible = true;
		_colorRectSplash.Visible = true;
		_waitingForClick = true;

		while (_waitingForClick)
		{
			await ToSignal(GetTree(), "process_frame");
		}

		_colorRectSplash.Visible = false;
	}

	private void OnSplashGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (_waitingForClick) _waitingForClick = false;
		}
	}

	private void PickRandomStatMode()
	{
		string[] stats = { "HP", "ATK", "DEF", "SPD", "SPATK", "SPDEF" };
		_currentStatMode = stats[GD.Randi() % stats.Length];
		_gameStatLabel.Text = _currentStatMode;
	}

	private async void OnHitPressed()
	{
		if (_isAnimating || _waitingForClick) return;
		
		int pokemonId = GameUtils.GetRandomIdByGenRange(1, _currentGeneration);
		// We pass the Parent (CardPlayer) and the Child (SpritePlayer)
		await AnimateCardDraw(_cardPlayer, _spritePlayer, pokemonId);

		_playerTotal += GetStatValue(pokemonId, _currentStatMode);
		_labelPlayerScore.Text = _playerTotal.ToString();

		if (_playerTotal > GoalScore) EndGame(false);
	}

	private async void OnStandPressed()
	{
		if (_isAnimating || _waitingForClick) return;
		SetButtonsEnabled(false); 

		while (_dealerTotal < DealerStopThreshold)
		{
			int pokemonId = GameUtils.GetRandomIdByGenRange(1, _currentGeneration);
			await AnimateCardDraw(_cardDealer, _spriteDealer, pokemonId);
			
			_dealerTotal += GetStatValue(pokemonId, _currentStatMode);
			_labelDealerScore.Text = _dealerTotal.ToString();
			
			await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
		}

		CheckWinner();
	}

	private async Task AnimateCardDraw(TextureRect cardContainer, TextureRect pokemonSprite, int pokemonId)
	{
		_isAnimating = true;
		
		_cardBack.GlobalPosition = _cardDeck.GlobalPosition;
		_cardBack.Visible = true;

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(_cardBack, "global_position", cardContainer.GlobalPosition, 0.3f)
			 .SetTrans(Tween.TransitionType.Quad)
			 .SetEase(Tween.EaseType.Out);

		await ToSignal(tween, "finished");

		_cardBack.Visible = false;

		// Set the sprite texture (the child)
		pokemonSprite.Texture = GameUtils.GetPokemonSprite(pokemonId);
		
		// Show the card container (the parent)
		cardContainer.Visible = true; 
		
		_isAnimating = false;
	}

	private void CheckWinner()
	{
		if (_playerTotal > GoalScore) EndGame(false);
		else if (_dealerTotal > GoalScore) EndGame(true);
		else if (_playerTotal > _dealerTotal) EndGame(true);
		else if (_playerTotal < _dealerTotal) EndGame(false);
		else EndGame(null);
	}

	private async void EndGame(bool? playerWon)
	{
		SetButtonsEnabled(false);
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

		if (playerWon == true) await ShowSplash(_splashWin);
		else if (playerWon == false) await ShowSplash(_splashLose);
		else await ShowSplash(_splashDraw);

		StartGame();
	}

	private void SetButtonsEnabled(bool enabled)
	{
		_buttonHit.Disabled = !enabled;
		_buttonStand.Disabled = !enabled;
		Color tint = enabled ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 1.0f);
		_buttonHit.SelfModulate = tint;
		_buttonStand.SelfModulate = tint;
	}

	private int GetStatValue(int id, string statName)
	{
		return statName switch
		{
			"HP" => PokemonData.HP[id],
			"ATK" => PokemonData.ATK[id],
			"DEF" => PokemonData.DEF[id],
			"SPD" => PokemonData.SPD[id],
			"SPATK" => PokemonData.SPATK[id],
			"SPDEF" => PokemonData.SPDEF[id],
			_ => 0
		};
	}

	public void SetGeneration(int gen) => _currentGeneration = gen;
	public void StopGame() { }
}
