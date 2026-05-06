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

	// Deck State
	private HashSet<int> _drawnPokemonIds = new HashSet<int>();

	private int _playerTotal = 0;
	private int _dealerTotal = 0;
	private bool _isAnimating = false;
	private bool _waitingForClick = false;

	public override void _Ready()
	{
		_cardDealer = GetNode<TextureRect>("%CardDealer");
		_cardPlayer = GetNode<TextureRect>("%CardPlayer");
		_spriteDealer = GetNode<TextureRect>("%SpriteDealer");
		_spritePlayer = GetNode<TextureRect>("%SpritePlayer");
		_cardBack = GetNode<TextureRect>("%CardBack");
		_cardDeck = GetNode<TextureRect>("CardDeck");
		_buttonHit = GetNode<TextureButton>("%TextureButtonHit");
		_buttonStand = GetNode<TextureButton>("%TextureButtonStand");
		_labelDealerScore = GetNode<Label>("%LabelDealerScore");
		_labelPlayerScore = GetNode<Label>("%LabelPlayerScore");
		_gameStatLabel = GetNode<Label>("%GameStatLabel");
		_colorRectSplash = GetNode<ColorRect>("%ColorRectSplash");
		_splashTitle = GetNode<TextureRect>("%SplashTitle");
		_splashWin = GetNode<TextureRect>("%SplashWin");
		_splashLose = GetNode<TextureRect>("%SplashLose");
		_splashDraw = GetNode<TextureRect>("%SplashDraw");

		_buttonHit.Pressed += OnHitPressed;
		_buttonStand.Pressed += OnStandPressed;
		_colorRectSplash.GuiInput += OnSplashGuiInput;

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
		_drawnPokemonIds.Clear();
		_cardDealer.Visible = false;
		_cardPlayer.Visible = false;
		SetButtonsEnabled(true);
	}

	private int DrawUniquePokemonId()
	{
		int pokemonId;
		do
		{
			pokemonId = GameUtils.GetRandomIdByGenRange(1, _currentGeneration);
		} 
		while (_drawnPokemonIds.Contains(pokemonId));

		_drawnPokemonIds.Add(pokemonId);
		return pokemonId;
	}

	private async void OnHitPressed()
	{
		if (_isAnimating || _waitingForClick) return;
		
		int pokemonId = DrawUniquePokemonId();
		await AnimateCardDraw(_cardPlayer, _spritePlayer, pokemonId);

		_playerTotal += GetStatValue(pokemonId, _currentStatMode);
		_labelPlayerScore.Text = _playerTotal.ToString();

		// Win/Loss immediate checks
		if (_playerTotal == GoalScore) 
		{
			EndGame(true); // Instant Blackjack Win!
		}
		else if (_playerTotal > GoalScore) 
		{
			EndGame(false); // Bust
		}
	}

	private async void OnStandPressed()
	{
		if (_isAnimating || _waitingForClick) return;
		SetButtonsEnabled(false); 

		while (_dealerTotal < DealerStopThreshold)
		{
			int pokemonId = DrawUniquePokemonId();
			await AnimateCardDraw(_cardDealer, _spriteDealer, pokemonId);
			
			_dealerTotal += GetStatValue(pokemonId, _currentStatMode);
			_labelDealerScore.Text = _dealerTotal.ToString();

			// If dealer hits exactly GoalScore, they stop immediately
			if (_dealerTotal >= GoalScore) break;
			
			await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
		}

		CheckWinner();
	}

	private void CheckWinner()
	{
		if (_playerTotal > GoalScore) EndGame(false);
		else if (_dealerTotal > GoalScore) EndGame(true);
		else if (_playerTotal > _dealerTotal) EndGame(true);
		else if (_playerTotal < _dealerTotal) EndGame(false);
		else EndGame(null); // Draw
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
		pokemonSprite.Texture = GameUtils.GetPokemonSprite(pokemonId);
		cardContainer.Visible = true; 
		_isAnimating = false;
	}

	private async Task ShowSplash(TextureRect activeSplash)
	{
		_splashTitle.Visible = false; _splashWin.Visible = false; _splashLose.Visible = false; _splashDraw.Visible = false;
		activeSplash.Visible = true; _colorRectSplash.Visible = true; _waitingForClick = true;
		while (_waitingForClick) await ToSignal(GetTree(), "process_frame");
		_colorRectSplash.Visible = false;
	}

	private void OnSplashGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			if (_waitingForClick) _waitingForClick = false;
	}

	private void PickRandomStatMode()
	{
		string[] stats = { "HP", "ATK", "DEF", "SPD", "SPATK", "SPDEF" };
		_currentStatMode = stats[GD.Randi() % stats.Length];
		_gameStatLabel.Text = _currentStatMode;
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
