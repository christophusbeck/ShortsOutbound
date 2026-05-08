using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public enum HandRank
{
	Singleton = 0,
	TwoOfFamily = 1,
	Twins = 2,
	TwoConsecutiveEvo = 3,
	ThreeOfFamily = 4,
	Triplets = 5,
	EvoFlush = 6,
	LegendaryTrio = 7
}

public partial class KantoHoldEm : Node2D, IBaseGame
{
	private int _currentGeneration = 1;
	private Dictionary<int, int[]> _evoData;

	private List<int> _playerHand = new List<int>();
	private List<int> _dealerHand = new List<int>();
	private bool[] _hasDiscarded = new bool[3];

	private TextureRect[] _dealerFronts = new TextureRect[3];
	private TextureRect[] _dealerBacks = new TextureRect[3];
	private TextureRect[] _dealerSprites = new TextureRect[3];
	private TextureButton[] _playerFronts = new TextureButton[3];
	private TextureRect[] _playerSprites = new TextureRect[3];

	private TextureRect _cardBack, _cardDeck;
	private TextureButton _showdownButton;
	private bool _isAnimating = false;
	private bool _canDiscard = false;

	public override void _Ready()
	{
		_evoData = GameUtils.LoadEvolutionData();
		_cardBack = GetNode<TextureRect>("%CardBack");
		_cardDeck = GetNode<TextureRect>("%CardDeck");
		_showdownButton = GetNode<TextureButton>("%ShowdownButton");

		for (int i = 0; i < 3; i++)
		{
			int num = i + 1;
			_dealerFronts[i] = GetNode<TextureRect>($"%CardDealerFront{num}");
			_dealerBacks[i] = GetNode<TextureRect>($"%CardDealerBack{num}");
			_dealerSprites[i] = GetNode<TextureRect>($"%CardDealerSprite{num}");
			_playerFronts[i] = GetNode<TextureButton>($"%CardPlayerFront{num}");
			_playerSprites[i] = GetNode<TextureRect>($"%CardPlayerSprite{num}");

			int index = i;
			_playerFronts[i].Pressed += () => OnCardDiscardPressed(index);
		}

		_showdownButton.Pressed += OnShowdownPressed;
		ResetTable();
	}

	public async void StartGame()
	{
		if (_isAnimating) return;
		ResetTable();
		
		for (int i = 0; i < 3; i++)
		{
			_playerHand.Add(GameUtils.GetRandomIdByGenRange(1, _currentGeneration));
			_dealerHand.Add(GameUtils.GetRandomIdByGenRange(1, _currentGeneration));
		}

		_playerHand.Sort();

		for (int i = 0; i < 3; i++)
		{
			await AnimateCardMovement(_playerFronts[i], _playerSprites[i], _playerHand[i], true);
			await AnimateCardMovement(_dealerBacks[i], _dealerSprites[i], _dealerHand[i], false);
		}

		_canDiscard = true;
		_showdownButton.Visible = true;
	}

	private async void OnCardDiscardPressed(int index)
	{
		if (!_canDiscard || _hasDiscarded[index] || _isAnimating) return;
		_isAnimating = true;
		_hasDiscarded[index] = true;

		_cardBack.GlobalPosition = _playerFronts[index].GlobalPosition;
		_cardBack.Visible = true;
		_playerFronts[index].Visible = false;

		Tween exitTween = GetTree().CreateTween();
		exitTween.TweenProperty(_cardBack, "global_position", _cardDeck.GlobalPosition, 0.2f);
		await ToSignal(exitTween, "finished");

		int newId = GameUtils.GetRandomIdByGenRange(1, _currentGeneration);
		_playerHand[index] = newId;
		_isAnimating = false; 
		await AnimateCardMovement(_playerFronts[index], _playerSprites[index], newId, true);

		_playerFronts[index].Modulate = new Color(0.7f, 0.7f, 0.7f);
	}

	private async void OnShowdownPressed()
	{
		if (_isAnimating) return;
		_canDiscard = false;
		_showdownButton.Visible = false;
		_isAnimating = true;

		// 1. Capture original positions before gathering
		Vector2[] originalPositions = new Vector2[3];
		for (int i = 0; i < 3; i++)
		{
			originalPositions[i] = _playerFronts[i].GlobalPosition;
		}

		// 2. Gather to the middle card
		Vector2 centerPos = _playerFronts[1].GlobalPosition;
		_playerHand.Sort();

		Tween gatherTween = GetTree().CreateTween().SetParallel(true);
		for (int i = 0; i < 3; i++)
		{
			gatherTween.TweenProperty(_playerFronts[i], "global_position", centerPos, 0.25f)
					   .SetTrans(Tween.TransitionType.Quad)
					   .SetEase(Tween.EaseType.Out);
		}
		await ToSignal(gatherTween, "finished");

		// 3. Update Textures & Reset Modulate while stacked
		for (int i = 0; i < 3; i++)
		{
			_playerSprites[i].Texture = GameUtils.GetPokemonSprite(_playerHand[i]);
			_playerFronts[i].Modulate = Colors.White;
		}

		// 4. Spread back out to original positions
		Tween spreadTween = GetTree().CreateTween().SetParallel(true);
		for (int i = 0; i < 3; i++)
		{
			spreadTween.TweenProperty(_playerFronts[i], "global_position", originalPositions[i], 0.3f)
					   .SetTrans(Tween.TransitionType.Back)
					   .SetEase(Tween.EaseType.Out);
		}
		await ToSignal(spreadTween, "finished");

		// 5. Dealer Reveal
		_dealerHand.Sort();
		for (int i = 0; i < 3; i++)
		{
			_dealerSprites[i].Texture = GameUtils.GetPokemonSprite(_dealerHand[i]);
			
			// Swap back for front
			_dealerBacks[i].Visible = false;
			_dealerFronts[i].Visible = true; 
			
			// Ensure the sprite (child of front) is visible
			_dealerSprites[i].Visible = true;

			await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
		}

		// ... after the dealer reveal loop ...
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout"); // Dramatic pause
		_isAnimating = false;
		EvaluateWinner();
	}

	private async Task AnimateCardMovement(Control targetNode, TextureRect spriteNode, int pokemonId, bool showFront)
	{
		_isAnimating = true;
		_cardBack.GlobalPosition = _cardDeck.GlobalPosition;
		_cardBack.Visible = true;

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(_cardBack, "global_position", targetNode.GlobalPosition, 0.25f)
			 .SetTrans(Tween.TransitionType.Quad)
			 .SetEase(Tween.EaseType.Out);

		await ToSignal(tween, "finished");
		_cardBack.Visible = false;

		spriteNode.Texture = GameUtils.GetPokemonSprite(pokemonId);
		targetNode.Visible = true; 
		_isAnimating = false;
	}

	private void ResetTable()
	{
		_playerHand.Clear();
		_dealerHand.Clear();
		_hasDiscarded = new bool[] { false, false, false };
		_canDiscard = false;
		_showdownButton.Visible = false;

		for (int i = 0; i < 3; i++)
		{
			_dealerFronts[i].Visible = false;
			_dealerBacks[i].Visible = false;
			_playerFronts[i].Visible = false;
			_playerFronts[i].Modulate = Colors.White;
			
			// Safety: Ensure they are in their correct slots on reset
			// If they are under a GridContainer or HBox, you might need to use Position instead of GlobalPosition
		}
	}

	public void SetGeneration(int gen) => _currentGeneration = gen;
	public void StopGame() => ResetTable();


	
	private bool CheckForEvolutionStraight(int id1, int id2, int id3)
	{
		// Sort IDs first so we check in order: Bulbasaur -> Ivysaur -> Venusaur
		int[] hand = { id1, id2, id3 };
		Array.Sort(hand);

		// Check 1 -> 2
		bool firstStep = _evoData.ContainsKey(hand[0]) && _evoData[hand[0]].Contains(hand[1]);
		// Check 2 -> 3
		bool secondStep = _evoData.ContainsKey(hand[1]) && _evoData[hand[1]].Contains(hand[2]);

		return firstStep && secondStep;
	}
	private bool HasConsecutivePair(int[] hand, Dictionary<int, int[]> evoData)
	{
		foreach (int parent in hand)
		{
			if (evoData.ContainsKey(parent))
			{
				foreach (int child in hand)
				{
					if (System.Linq.Enumerable.Contains(evoData[parent], child))
						return true;
				}
			}
		}
		return false;
	}
	private bool HasSkippedEvolution(int[] hand, Dictionary<int, int[]> evoData)
	{
		foreach (int grandparent in hand)
		{
			if (!evoData.ContainsKey(grandparent)) continue;

			// Look at all possible children of the grandparent
			foreach (int middleStage in evoData[grandparent])
			{
				// If the middle stage exists in the data and has its own children...
				if (evoData.ContainsKey(middleStage))
				{
					// Check if any of those children (the grandchildren) are in our hand
					foreach (int grandchild in hand)
					{
						if (System.Linq.Enumerable.Contains(evoData[middleStage], grandchild))
							return true;
					}
				}
			}
		}
		return false;
	}
	private int GetBST(int id)
	{
		if (id <= 0) return 0;
		// Summing all base stats from our data source
		return PokemonData.HP[id] + 
			   PokemonData.ATK[id] + 
			   PokemonData.DEF[id] + 
			   PokemonData.SPATK[id] + 
			   PokemonData.SPDEF[id] + 
			   PokemonData.SPD[id];
	}

	private bool IsLegendaryTrio(int id1, int id2, int id3)
	{
		int[] hand = { id1, id2, id3 };
		Array.Sort(hand);

		// Example: Articuno (144), Zapdos (145), Moltres (146)
		if (hand[0] == 144 && hand[1] == 145 && hand[2] == 146) return true;
		
		// Example: Entei (244), Raikou (243), Suicune (245) - Needs sorting check
		if (hand[0] == 243 && hand[1] == 244 && hand[2] == 245) return true;

		// Example: Regirock (377), Regice (378), Registeel (379)
		if (hand[0] == 377 && hand[1] == 378 && hand[2] == 379) return true;

		return false;
	}

	private bool AreSameFamily(int[] hand)
	{
		if (hand.Length < 2) return false;
		int firstBase = FindBaseForm(hand[0], _evoData);
		return hand.All(id => FindBaseForm(id, _evoData) == firstBase);
	}

	private int FindBaseForm(int pokemonId, Dictionary<int, int[]> evoData)
	{
		foreach (var entry in evoData)
		{
			if (entry.Value.Contains(pokemonId))
				return FindBaseForm(entry.Key, evoData);
		}
		return pokemonId;
	}

	private (HandRank rank, int tieBreakerBST) EvaluateHand(List<int> handList)
	{
		int[] hand = handList.ToArray();
		Array.Sort(hand);

		// 1. Legendary Trio
		if (IsLegendaryTrio(hand[0], hand[1], hand[2]))
			return (HandRank.LegendaryTrio, hand.Max(id => GetBST(id)));

		// 2. Evo Flush (Three consecutive)
		if (CheckForEvolutionStraight(hand[0], hand[1], hand[2]))
			return (HandRank.EvoFlush, GetBST(hand[2])); // Highest evo in straight is tiebreaker

		// 3. Triplets (Three of the same)
		if (hand[0] == hand[1] && hand[1] == hand[2])
			return (HandRank.Triplets, GetBST(hand[0]));

		// 4. Three of the same family
		if (AreSameFamily(hand))
			return (HandRank.ThreeOfFamily, hand.Max(id => GetBST(id)));

		// 5. Two consecutive evolutions
		// We need to find which two are consecutive for the tiebreaker
		if (IsConsecutive(hand[0], hand[1])) return (HandRank.TwoConsecutiveEvo, Math.Max(GetBST(hand[0]), GetBST(hand[1])));
		if (IsConsecutive(hand[1], hand[2])) return (HandRank.TwoConsecutiveEvo, Math.Max(GetBST(hand[1]), GetBST(hand[2])));
		if (IsConsecutive(hand[0], hand[2])) return (HandRank.TwoConsecutiveEvo, Math.Max(GetBST(hand[0]), GetBST(hand[2])));

		// 6. Twins
		if (hand[0] == hand[1] || hand[1] == hand[2] || hand[0] == hand[2])
		{
			int twinId = (hand[0] == hand[1]) ? hand[0] : hand[1]; // Simple check since sorted
			return (HandRank.Twins, GetBST(twinId));
		}

		// 7. Two of same family
		int familyId = FindFamilyMatch(hand);
		if (familyId != -1)
			return (HandRank.TwoOfFamily, familyId); // familyId is already the BST of the best member

		// 8. Singleton
		return (HandRank.Singleton, hand.Max(id => GetBST(id)));
	}

	// Helper for Two Consecutive
	private bool IsConsecutive(int a, int b)
	{
		return (_evoData.ContainsKey(a) && _evoData[a].Contains(b)) || 
			   (_evoData.ContainsKey(b) && _evoData[b].Contains(a));
	}

	// Helper for Two of Family tiebreaker
	private int FindFamilyMatch(int[] hand)
	{
		int b0 = FindBaseForm(hand[0], _evoData);
		int b1 = FindBaseForm(hand[1], _evoData);
		int b2 = FindBaseForm(hand[2], _evoData);

		if (b0 == b1) return Math.Max(GetBST(hand[0]), GetBST(hand[1]));
		if (b1 == b2) return Math.Max(GetBST(hand[1]), GetBST(hand[2]));
		if (b0 == b2) return Math.Max(GetBST(hand[0]), GetBST(hand[2]));
		
		return -1;
	}
	
	private void EvaluateWinner()
	{
		var playerResult = EvaluateHand(_playerHand);
		var dealerResult = EvaluateHand(_dealerHand);

		GD.Print($"--- Results ---");
		GD.Print($"Player: {playerResult.rank} (Tie-breaker BST: {playerResult.tieBreakerBST})");
		GD.Print($"Dealer: {dealerResult.rank} (Tie-breaker BST: {dealerResult.tieBreakerBST})");

		if (playerResult.rank > dealerResult.rank)
		{
			GD.Print(">>> PLAYER WINS (Higher Figure) <<<");
		}
		else if (playerResult.rank < dealerResult.rank)
		{
			GD.Print(">>> DEALER WINS (Higher Figure) <<<");
		}
		else
		{
			// Ranks are equal, check tie-breaker BST
			if (playerResult.tieBreakerBST > dealerResult.tieBreakerBST)
			{
				GD.Print(">>> PLAYER WINS (Tie-breaker BST) <<<");
			}
			else if (playerResult.tieBreakerBST < dealerResult.tieBreakerBST)
			{
				GD.Print(">>> DEALER WINS (Tie-breaker BST) <<<");
			}
			else
			{
				GD.Print(">>> IT'S A TIE! <<<");
			}
		}
	}

}
