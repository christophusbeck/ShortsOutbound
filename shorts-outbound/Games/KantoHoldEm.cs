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
	private float _dealerEdge = 0.003f; //Dealer's chance of getting the best hand, cascading down

	private TextureRect[] _dealerFronts = new TextureRect[3];
	private TextureRect[] _dealerBacks = new TextureRect[3];
	private TextureRect[] _dealerSprites = new TextureRect[3];
	private TextureButton[] _playerFronts = new TextureButton[3];
	private TextureRect[] _playerSprites = new TextureRect[3];

	private TextureRect _cardBack, _cardDeck;
	private TextureButton _showdownButton;
	private TextureButton _splashStart, _splashWin, _splashDraw, _splashLose;
	
	private bool _isAnimating = false;
	private bool _canDiscard = false;

	public override void _Ready()
	{
		_evoData = GameUtils.LoadEvolutionData();
		_cardBack = GetNode<TextureRect>("%CardBack");
		_cardDeck = GetNode<TextureRect>("%CardDeck");
		_showdownButton = GetNode<TextureButton>("%ShowdownButton");
		
		_splashStart = GetNode<TextureButton>("%SplashStart");
		_splashWin = GetNode<TextureButton>("%SplashWin");
		_splashDraw = GetNode<TextureButton>("%SplashDraw");
		_splashLose = GetNode<TextureButton>("%SplashLose");

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
		
		// Hide end-game splashes at start
		_splashWin.Visible = false;
		_splashDraw.Visible = false;
		_splashLose.Visible = false;

		// Show start splash
		_splashStart.Visible = true;

		// Connect splash signals
		_splashStart.Pressed += OnSplashStartPressed;
		_splashWin.Pressed += OnRestartGamePressed;
		_splashDraw.Pressed += OnRestartGamePressed;
		_splashLose.Pressed += OnRestartGamePressed;
		
		//ResetTable();
	}
	private void OnSplashStartPressed()
	{
		_splashStart.Visible = false;
		StartGame(); // This kicks off the first deal
	}

	private void OnRestartGamePressed()
	{
		// Hide all possible end splashes
		_splashWin.Visible = false;
		_splashDraw.Visible = false;
		_splashLose.Visible = false;
		
		StartGame(); // Reset and deal new cards
	}

	public async void StartGame()
	{
		if (_isAnimating) return;
		ResetTable();
		
		_dealerEdge = GetDynamicDealerEdge(_dealerEdge, _currentGeneration);
		
		// Player still gets a standard random hand
		for (int i = 0; i < 3; i++)
		{
			_playerHand.Add(GameUtils.GetRandomIdByGenRange(1, _currentGeneration));
		}
		_playerHand.Sort();

		// FIXED: Dealer now uses the new cascading logic
		_dealerHand = GenerateDealerHand();

		// Animate the dealing process
		for (int i = 0; i < 3; i++)
		{
			await AnimateCardMovement(_playerFronts[i], _playerSprites[i], _playerHand[i], true);
			await AnimateCardMovement(_dealerBacks[i], _dealerSprites[i], _dealerHand[i], false);
		}

		_canDiscard = true;
		_showdownButton.Visible = true;
	}
	
	private List<int> GenerateDealerHand()
	{
		// We iterate from highest rank to lowest
		// HandRank is an enum, so we can cast from int
		for (int r = (int)HandRank.LegendaryTrio; r > (int)HandRank.Singleton; r--)
		{
			if (GD.Randf() < _dealerEdge)
			{
				return GenerateSpecificRank((HandRank)r);
			}
			_dealerEdge *= 1.03f; //give dealer better chances to roll lower but still good hands
		}
		
		// Default: Just a random hand (Singleton or whatever nature provides)
		return new List<int> { 
			GameUtils.GetRandomIdByGenRange(1, _currentGeneration),
			GameUtils.GetRandomIdByGenRange(1, _currentGeneration),
			GameUtils.GetRandomIdByGenRange(1, _currentGeneration)
		};
	}
	
	
	
	private List<int> GetAllFamilyMembers(int baseId)
	{
		// Access the static dictionary directly to get the Max for the current generation
		int maxId = GameUtils.GenBounds[_currentGeneration].Max;
		
		List<int> members = new List<int>();
		
		// Only add if it's within the current gen range
		if (baseId <= maxId)
		{
			members.Add(baseId);
		}
		else return members; // If base is too high, the whole family is too high
		
		if (_evoData.ContainsKey(baseId))
		{
			foreach (int child in _evoData[baseId])
			{
				// Only recurse if the child is within the current gen
				if (child <= maxId)
				{
					var childFamily = GetAllFamilyMembers(child);
					foreach(var member in childFamily)
					{
						if (!members.Contains(member)) members.Add(member);
					}
				}
			}
		}
		return members.Distinct().ToList();
	}

	private int GetRandomBaseWithEvolutions(int requiredEvos)
	{
		// Filter _evoData for entries that lead to a chain of 'requiredEvos'
		// For simplicity, you could pre-calculate this list in _Ready
		var candidates = _evoData.Keys.Where(k => _evoData[k].Length > 0).ToList();
		return candidates[(int)GD.Randi() % (int)candidates.Count];
	}
	
	private float GetDynamicDealerEdge(float baseEdge, int activeGens)
	{
		float basePoolSize = GameUtils.GenBounds[1].Max; // 151
		float currentPoolSize = GameUtils.GenBounds[activeGens].Max;

		// 1. Calculate the ratio
		float ratio = basePoolSize / currentPoolSize;

		// 2. Apply Power Decay 
		float scalingFactor = Mathf.Pow(ratio, 1.05f); 

		float calculatedEdge = baseEdge * scalingFactor;

		// 3. Optional: Set a hard minimum so the dealer isn't 100% useless
		return Mathf.Max(calculatedEdge, 0.005f); 
	}
	
	private List<int> GenerateSpecificRank(HandRank rank)
	{
		List<int> hand = new List<int>();
		int gen = _currentGeneration;
		// Get the generation cap directly from the static dictionary
		int maxId = GameUtils.GenBounds[gen].Max;
		
		bool found = false;
		int attempts = 0;
		const int MAX_ATTEMPTS = 1000;

		switch (rank)
		{
			case HandRank.LegendaryTrio:
				// Check gen to ensure birds don't appear in later-only setups 
				// or ensure gen 1 logic
				int[][] trios = { new[] { 144, 145, 146 } }; 
				int[] selectedTrio = trios[GD.Randi() % trios.Length];
				hand.AddRange(selectedTrio);
				break;

			case HandRank.EvoFlush:
				while (!found && attempts < MAX_ATTEMPTS)
				{
					attempts++;
					int id = GameUtils.GetRandomIdByGenRange(1, gen);
					// 1. Check if base exists and has children
					if (_evoData.ContainsKey(id) && id <= maxId && _evoData[id].Length > 0)
					{
						foreach (int child in _evoData[id])
						{
							// 2. Check if child is within gen and HAS its own children (grandchild)
							if (child <= maxId && _evoData.ContainsKey(child) && _evoData[child].Length > 0)
							{
								int grandchild = _evoData[child][(int)(GD.Randi() % (uint)_evoData[child].Length)];
								
								if (grandchild <= maxId)
								{
									hand.AddRange(new[] { id, child, grandchild });
									found = true;
									break;
								}
							}
						}
					}
				}
				break;

			case HandRank.ThreeOfFamily:
				while (!found && attempts < MAX_ATTEMPTS)
				{
					attempts++;
					int id = GameUtils.GetRandomIdByGenRange(1, gen);
					int baseForm = FindBaseForm(id, _evoData);
					List<int> family = GetAllFamilyMembers(baseForm);
					if (family.Count >= 3)
					{
						hand = family.OrderBy(x => GD.Randi()).Take(3).ToList();
						found = true;
					}
				}
				break;

			case HandRank.TwoConsecutiveEvo:
				while (!found && attempts < MAX_ATTEMPTS)
				{
					attempts++;
					int id = GameUtils.GetRandomIdByGenRange(1, gen);
					
					// Add check: id must exist AND have at least one evolution
					if (_evoData.ContainsKey(id) && id <= maxId && _evoData[id].Length > 0)
					{
						// Now it is safe to use modulo because Length is at least 1
						int child = _evoData[id][(int)(GD.Randi() % (uint)_evoData[id].Length)];
						
						if (child <= maxId)
						{
							hand.Add(id);
							hand.Add(child);
							hand.Add(GameUtils.GetRandomIdByGenRange(1, gen));
							found = true;
						}
					}
				}
				break;

			case HandRank.TwoOfFamily:
				while (!found && attempts < MAX_ATTEMPTS)
				{
					attempts++;
					int id = GameUtils.GetRandomIdByGenRange(1, gen);
					int baseForm = FindBaseForm(id, _evoData);
					List<int> family = GetAllFamilyMembers(baseForm);
					if (family.Count >= 2)
					{
						hand = family.OrderBy(x => GD.Randi()).Take(2).ToList();
						hand.Add(GameUtils.GetRandomIdByGenRange(1, gen));
						found = true;
					}
				}
				break;

			case HandRank.Triplets:
				int tripletId = GameUtils.GetRandomIdByGenRange(1, gen);
				hand.AddRange(new[] { tripletId, tripletId, tripletId });
				break;

			case HandRank.Twins:
				int twinId = GameUtils.GetRandomIdByGenRange(1, gen);
				hand.AddRange(new[] { twinId, twinId, GameUtils.GetRandomIdByGenRange(1, gen) });
				break;

			default:
				for (int i = 0; i < 3; i++)
					hand.Add(GameUtils.GetRandomIdByGenRange(1, gen));
				break;
		}

		// Fallback logic
		if (hand.Count < 3)
		{
			for (int i = hand.Count; i < 3; i++)
				hand.Add(GameUtils.GetRandomIdByGenRange(1, gen));
		}

		return hand;
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
			// Hide elements
			_dealerFronts[i].Visible = false;
			_dealerBacks[i].Visible = false;
			_playerFronts[i].Visible = false;
			
			// --- NEW: Reset Highlights and Interaction States ---
			_playerFronts[i].SelfModulate = Colors.White;
			_playerFronts[i].Modulate = Colors.White; // Reset the "dimmed" look from discards
			_playerFronts[i].Disabled = false;
			
			_dealerFronts[i].SelfModulate = Colors.White;
			
			// Ensure cards are back in their slots if they were moved during Showdown
			_playerFronts[i].GlobalPosition = GetNode<Control>($"%CardPlayerFront{i+1}").GlobalPosition;
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
	
	// Add a helper method to apply the highlight
	private void ApplyHighlights(CanvasItem[] cardNodes, bool[] highlightMask)
	{
		Color highlightColor = new Color(1.2f, 1.2f, 0.8f); // A slight glow/yellow tint
		for (int i = 0; i < 3; i++)
		{
			cardNodes[i].SelfModulate = highlightMask[i] ? highlightColor : Colors.White;
		}
	}

	
	private (HandRank rank, int tieBreakerBST, bool[] mask) EvaluateHand(List<int> handList)
	{
		int[] hand = handList.ToArray();
		// IMPORTANT: hand is already sorted from OnShowdownPressed
		bool[] mask = new bool[3];

		// 1. Legendary Trio
		if (IsLegendaryTrio(hand[0], hand[1], hand[2]))
			return (HandRank.LegendaryTrio, hand.Max(id => GetBST(id)), new bool[] { true, true, true });

		// 2. Evo Flush
		if (CheckForEvolutionStraight(hand[0], hand[1], hand[2]))
			return (HandRank.EvoFlush, GetBST(hand[2]), new bool[] { true, true, true });

		// 3. Triplets
		if (hand[0] == hand[1] && hand[1] == hand[2])
			return (HandRank.Triplets, GetBST(hand[0]), new bool[] { true, true, true });

		// 4. Three of Family
		if (AreSameFamily(hand))
			return (HandRank.ThreeOfFamily, hand.Max(id => GetBST(id)), new bool[] { true, true, true });

		// 5. Two Consecutive
		if (IsConsecutive(hand[0], hand[1])) { mask[0] = true; mask[1] = true; return (HandRank.TwoConsecutiveEvo, Math.Max(GetBST(hand[0]), GetBST(hand[1])), mask); }
		if (IsConsecutive(hand[1], hand[2])) { mask[1] = true; mask[2] = true; return (HandRank.TwoConsecutiveEvo, Math.Max(GetBST(hand[1]), GetBST(hand[2])), mask); }
		if (IsConsecutive(hand[0], hand[2])) { mask[0] = true; mask[2] = true; return (HandRank.TwoConsecutiveEvo, Math.Max(GetBST(hand[0]), GetBST(hand[2])), mask); }

		// 6. Twins
		if (hand[0] == hand[1]) { mask[0] = true; mask[1] = true; return (HandRank.Twins, GetBST(hand[0]), mask); }
		if (hand[1] == hand[2]) { mask[1] = true; mask[2] = true; return (HandRank.Twins, GetBST(hand[1]), mask); }
		if (hand[0] == hand[2]) { mask[0] = true; mask[2] = true; return (HandRank.Twins, GetBST(hand[0]), mask); }

		// 7. Two of Family
		int b0 = FindBaseForm(hand[0], _evoData);
		int b1 = FindBaseForm(hand[1], _evoData);
		int b2 = FindBaseForm(hand[2], _evoData);
		if (b0 == b1) { mask[0] = true; mask[1] = true; return (HandRank.TwoOfFamily, Math.Max(GetBST(hand[0]), GetBST(hand[1])), mask); }
		if (b1 == b2) { mask[1] = true; mask[2] = true; return (HandRank.TwoOfFamily, Math.Max(GetBST(hand[1]), GetBST(hand[2])), mask); }
		if (b0 == b2) { mask[0] = true; mask[2] = true; return (HandRank.TwoOfFamily, Math.Max(GetBST(hand[0]), GetBST(hand[2])), mask); }

		// 8. Singleton - Highlight the highest BST card
		int maxIdx = 0;
		int maxBST = -1;
		for(int i=0; i<3; i++) {
			int current = GetBST(hand[i]);
			if(current > maxBST) { maxBST = current; maxIdx = i; }
		}
		mask[maxIdx] = true;
		return (HandRank.Singleton, maxBST, mask);
	}

	
	private async void EvaluateWinner()
	{
		var pResult = EvaluateHand(_playerHand);
		var dResult = EvaluateHand(_dealerHand);

		ApplyHighlights(_playerFronts, pResult.mask);
		ApplyHighlights(_dealerFronts, dResult.mask);

		// Wait 1.5 seconds so the player can actually see the "Showdown" results
		await ToSignal(GetTree().CreateTimer(1.5f), "timeout");

		if (pResult.rank > dResult.rank) 
		{
			_splashWin.Visible = true;
		}
		else if (pResult.rank < dResult.rank) 
		{
			_splashLose.Visible = true;
		}
		else 
		{
			if (pResult.tieBreakerBST > dResult.tieBreakerBST) 
				_splashWin.Visible = true;
			else if (pResult.tieBreakerBST < dResult.tieBreakerBST) 
				_splashLose.Visible = true;
			else 
				_splashDraw.Visible = true;
		}
	}

}
