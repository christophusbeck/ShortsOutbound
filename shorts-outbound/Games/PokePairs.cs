using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokePairs : Node2D, IBaseGame
{
	[Export] public int Rows = 4;
	[Export] public int Cols = 5; 
	[Export] public Vector2 CardSize = new Vector2(80, 110);
	[Export] public float Spacing = 10f;
	private Vector2 _dynamicGridMargin = new Vector2(10, 10); // Distance from Top-Left corner
	[Export] public float FixedYOffset = 25f;
	
	private bool _isInitialStart = true;

	private int _currentGen = 1;
	private List<TextureButton> _activeCards = new List<TextureButton>();
	private TextureButton _firstSelected = null;
	private TextureButton _secondSelected = null;
	private bool _isProcessing = false;
	private int _pairsFound = 0;
	private int _totalPairs = 0;
	
	private int _lives = 0;


	// Node References
	private TextureButton _cardBackTemplate;
	private TextureButton _cardFrontTemplate;
	private TextureButton _cardDeckNode;
	private Label _livesLabel;
	
	private TextureButton _splashTitle;
	private TextureButton _splashWin;
	private TextureButton _splashLose;

	public override void _Ready()
	{
		CalculateGridMargin();
		
		_cardBackTemplate = GetNode<TextureButton>("%CardBack");
		_cardFrontTemplate = GetNode<TextureButton>("%CardFront");
		_cardDeckNode = GetNode<TextureButton>("%CardDeck");
		_livesLabel = GetNode<Label>("%LivesLeftLabel");
		
		_splashTitle = GetNode<TextureButton>("%SplashTitle");
		_splashWin = GetNode<TextureButton>("%SplashWin");
		_splashLose = GetNode<TextureButton>("%SplashLose");

		// Ensure splashes stay on top of dynamically generated cards
		_splashTitle.ZIndex = 10;
		_splashWin.ZIndex = 10;
		_splashLose.ZIndex = 10;

		_splashTitle.Pressed += StartActualGame; // New separate method
		_splashWin.Pressed += ShowTitle;
		_splashLose.Pressed += ShowTitle;

		_cardBackTemplate.Hide();
		_cardFrontTemplate.Hide();

		ShowTitle();
	}
	
	public void ShowTitle()
	{
		ClearBoard();
		_splashWin.Hide();
		_splashLose.Hide();
		_splashTitle.Show();
	}

	// This is called by your Title Splash click
	private void StartActualGame()
	{
		_splashTitle.Hide();
		InitializeGameBoard();
	}

	// IBaseGame Implementation
	public void StartGame()
	{
		// If this is the very first time the MainApp calls this (on scene load),
		// we ignore it and stay on the Title Splash.
		if (_isInitialStart)
		{
			_isInitialStart = false;
			ShowTitle(); 
			return;
		}

		// If it's a manual reset (subsequent calls), we skip title and go to game
		_splashTitle.Hide(); 
		_splashWin.Hide();
		_splashLose.Hide();
		InitializeGameBoard();
	}

	private void InitializeGameBoard()
	{
		ClearBoard();
		
		_pairsFound = 0;
		_totalPairs = (Rows * Cols) / 2;
		_lives = (int)(Rows * Cols * 0.75f);
		UpdateLivesUI();
		
		int pairCount = _totalPairs;
		List<int> selectedIds = new List<int>();

		while (selectedIds.Count < pairCount)
		{
			int id = GameUtils.GetRandomIdByGenRange(1, _currentGen);
			if (!selectedIds.Contains(id)) selectedIds.Add(id);
		}

		List<int> gameBoardIds = new List<int>();
		gameBoardIds.AddRange(selectedIds);
		gameBoardIds.AddRange(selectedIds);
		gameBoardIds.Shuffle();

		GenerateGrid(gameBoardIds);
	}

	private void OnWin()
	{
		_isProcessing = true;
		_splashWin.Show();
		// Force splash to the front of the draw list
		_splashWin.ZIndex = 10; 
	}

	private void OnGameOver()
	{
		_isProcessing = true;
		_splashLose.Show();
		_splashLose.ZIndex = 10;
	}

	private void ClearBoard()
	{
		foreach (var card in _activeCards) if (IsInstanceValid(card)) card.QueueFree();
		_activeCards.Clear();
		_firstSelected = null;
		_secondSelected = null;
		_isProcessing = false; 
	}


	
	private void CalculateGridMargin()
	{
		// Calculate total width: (Cards * Width) + (Gaps * Spacing)
		float totalRowWidth = (Cols * CardSize.X) + ((Cols - 1) * Spacing);
		
		// Center it: (WindowWidth - RowWidth) / 2
		float startX = (480f - totalRowWidth) / 2f;

		_dynamicGridMargin = new Vector2(startX, FixedYOffset);
	}


	
	private void UpdateLivesUI()
	{
		if (_livesLabel != null)
		{
			_livesLabel.Text = _lives.ToString();
		}

	}
	

	private void GenerateGrid(List<int> idList)
	{
		// We no longer need to calculate boardSize or startPos based on the screen center.
		// The start position is simply our Margin.
		Vector2 startPos = _dynamicGridMargin;

		int index = 0;
		for (int r = 0; r < Rows; r++)
		{
			for (int c = 0; c < Cols; c++)
			{
				// Calculate position for this specific card
				Vector2 targetPos = startPos + new Vector2(
					c * (CardSize.X + Spacing), 
					r * (CardSize.Y + Spacing)
				);
				
				CreateCard(targetPos, idList[index]);
				index++;
			}
		}
	}

	private void CreateCard(Vector2 targetPos, int pokemonId)
	{
		// Setup the Card (Back)
		TextureButton newCard = (TextureButton)_cardBackTemplate.Duplicate();
		newCard.Visible = true;
		newCard.Position = _cardDeckNode.Position;
		
		// Store the ID in the Metadata so we can check for matches later
		newCard.SetMeta("poke_id", pokemonId);

		// Setup the Sprite (Front)
		// We duplicate the Front template and add it as a child of the Back
		TextureButton frontSide = (TextureButton)_cardFrontTemplate.Duplicate();

		// FIX 1: Reset local position to (0,0) so it aligns perfectly with the parent CardBack
		frontSide.Position = Vector2.Zero;

		// FIX 2: Ensure the front side doesn't eat the mouse clicks intended for the back side
		frontSide.MouseFilter = Control.MouseFilterEnum.Ignore;

		// Access the child directly since it's a child of frontSide
		TextureRect sprite = frontSide.GetNode<TextureRect>("CardFrontSprite"); 
		
		sprite.Texture = GameUtils.GetPokemonSprite(pokemonId);
		
		frontSide.Visible = false; // Start hidden
		newCard.AddChild(frontSide);
		
		AddChild(newCard);
		_activeCards.Add(newCard);

		// Tween to position
		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(newCard, "position", targetPos, 0.4f)
			 .SetTrans(Tween.TransitionType.Quad)
			 .SetEase(Tween.EaseType.Out);

		newCard.Pressed += () => OnCardPressed(newCard);
	}

	private async void OnCardPressed(TextureButton card)
	{
		if (_isProcessing || card == _firstSelected || card.Disabled) return;

		ShowCardFace(card, true);

		if (_firstSelected == null)
		{
			_firstSelected = card;
		}
		else
		{
			_secondSelected = card;
			await CheckMatch();
		}
	}

	private async Task CheckMatch()
	{
		_isProcessing = true;
		await ToSignal(GetTree().CreateTimer(0.8f), "timeout");

		int id1 = (int)_firstSelected.GetMeta("poke_id");
		int id2 = (int)_secondSelected.GetMeta("poke_id");

		if (id1 == id2)
		{
			_pairsFound++;
			_firstSelected.Disabled = true;
			_secondSelected.Disabled = true;
			
			if (_pairsFound >= _totalPairs)
			{
				OnWin();
				return; // Exit early so we don't reset _isProcessing yet
			}
		}
		else
		{
			_lives--;
			UpdateLivesUI();

			if (_lives <= 0)
			{
				OnGameOver();
				return; // Exit early
			}

			ShowCardFace(_firstSelected, false);
			ShowCardFace(_secondSelected, false);
		}

		_firstSelected = null;
		_secondSelected = null;
		_isProcessing = false;
	}

	private void ShowCardFace(TextureButton card, bool show)
	{
		// FIX 2: Safer way to find the front side
		var frontSide = card.GetChild<Control>(0);
		frontSide.Visible = show;
		
		// Instead of null, let's toggle the visibility of the "Back" texture 
		// by modulating or just swapping properly.
		// If you want to "hide" the back texture:
		card.SelfModulate = show ? new Color(1, 1, 1, 0) : new Color(1, 1, 1, 1);
	}
		


	public void SetGeneration(int gen) => _currentGen = gen;
	public void StopGame() => ClearBoard();

}
