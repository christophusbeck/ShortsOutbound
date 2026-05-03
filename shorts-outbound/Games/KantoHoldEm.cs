using Godot;
using System;

public partial class KantoHoldEm : Node2D, IBaseGame
{
	private int _currentGeneration;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	
	public void StartGame()
	{
		GD.Print("Game Logic Starting Now!");
		// Initialize your cards, spawners, or timers here
	}

	// This is called by MainApp.cs when the slider moves 
	// OR immediately before StartGame()
	public void SetGeneration(int gen)
	{
		_currentGeneration = gen;
		GD.Print($"Game updated to Generation: {gen}");
		// Update your game visuals here
	}

	public void StopGame()
	{
		GD.Print("Cleaning up game state...");
	}
}
using Godot;
using System;

public partial class KantoHoldEm : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
