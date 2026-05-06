using Godot;
using System;
using System.Collections.Generic;

public static class GameUtils
{
	// --- PATH CONSTANTS ---
	private const string SpritePath = "res://Assets/Pokesprites/";
	private const string FallbackImage = "0000.png";
	
	// --- Generations ---
	// A mapping of generation number to (MinID, MaxID)
	private static readonly Dictionary<int, (int Min, int Max)> GenBounds = new()
	{
		{ 1, (1, 151) },
		{ 2, (152, 251) },
		{ 3, (252, 386) },
		{ 4, (387, 493) },
		{ 5, (494, 649) },
		{ 6, (650, 721) },
		{ 7, (722, 809) },
		{ 8, (810, 1010) }
	};

	// --- ASSET LOADING ---

	public static Texture2D GetPokemonSprite(int pokedexNumber)
	{
		string fileName = pokedexNumber.ToString("D4") + ".png";
		string fullPath = SpritePath + fileName;

		if (ResourceLoader.Exists(fullPath))
		{
			return GD.Load<Texture2D>(fullPath);
		}

		return GD.Load<Texture2D>(SpritePath + FallbackImage);
	}

	// --- SHARED GAME LOGIC ---

	/// <summary>
	/// Returns a random Pokemon ID between the lower and upper generation bounds (inclusive).
	/// Defaults to full National Dex if inputs are invalid.
	/// </summary>
	public static int GetRandomIdByGenRange(int lowerGen, int upperGen)
	{
		// 1. Validate that the generations exist in our dictionary
		// 2. Ensure the lower bound isn't higher than the upper bound
		if (!GenBounds.ContainsKey(lowerGen) || 
			!GenBounds.ContainsKey(upperGen) || 
			lowerGen > upperGen)
		{
			// National Dex fallback (Gen 1 Min to Gen 8 Max)
			return GD.RandRange(1, 1010);
		}

		int minId = GenBounds[lowerGen].Min;
		int maxId = GenBounds[upperGen].Max;

		return GD.RandRange(minId, maxId);
	}

	/// <summary>
	/// Shuffles a list using the Fisher-Yates algorithm. 
	/// Great for Card Games or randomizing order.
	/// </summary>
	public static void Shuffle<T>(this IList<T> list)
	{
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = (int)(GD.Randi() % (n + 1));
			T value = list[k];
			list[k] = list[n];
			list[n] = value;
		}
	}


}
