namespace MonsterSpeed;

public static class GameTimer
{
    public static int Timer { get; private set; }
    public static void Update()
    {
        Timer = (Timer + 1) % int.MaxValue;
    }
}