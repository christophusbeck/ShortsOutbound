using Godot;
using System;
using System.Collections.Generic;

public static class GameUtils
{
	// --- PATH CONSTANTS ---
	private const string SpritePath = "res://Assets/Pokesprites/";
	private const string FallbackImage = "0000.png";

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
	/// Returns a random Pokemon ID based on the selected generation.
	/// </summary>
	public static int GetRandomIdByGen(int generation)
	{
		return generation switch
		{
			1 => GD.RandRange(1, 151),
			2 => GD.RandRange(152, 251),
			3 => GD.RandRange(252, 386),
			_ => GD.RandRange(1, 1010) // National Dex fallback
		};
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
