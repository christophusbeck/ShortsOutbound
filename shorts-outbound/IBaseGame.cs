using Godot;
public interface IBaseGame
{
	void StartGame();
	void SetGeneration(int gen);
	void StopGame(); // For future save-state logic
	
}
