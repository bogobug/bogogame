using UnityEngine;

public struct Move
{
    public Action action { get; private set; }

    public Vector2Int? direction { get; private set; }

    public Move(Action action, Vector2Int? direction)
    {
        this.action = action;
        this.direction = direction;
    }

    public override string ToString()
    {
        return action + (direction != null ? ": " + direction : "");
    }
}

public enum Action
{
    wait = 0,
    move = 1
}