using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameState
{
    // piece positions
    public Dictionary<Piece, Vector2Int> positions;

    // rotater dictionary
    public Dictionary<Vector2Int, int> rotaters;

    // orientation of board
    public Vector2Int orientation;

    // whether the board is solved;
    public bool solved;
}

public class GameStateController 
{
    // history of board states
    Stack<GameState> stateHistory = new Stack<GameState>();

    // initial state
    GameState initialState;

    // current state
    GameState currentState;

    // save the current board state
    public void saveState(GameState gameState) 
    {
        if (currentState != null)
        {
            stateHistory.Push(currentState);
        }
        
        if (initialState == null)
        {
            initialState = gameState;
        }

        currentState = gameState;
    }

    // retrieve the previous state and make it the current state
    // returns null if there is no previous state
    public GameState undoState()
    {
        if (stateHistory.Count == 0) { return null; }

        GameState prevState = stateHistory.Pop();
        currentState = prevState;
        return prevState;
    }

    // retrieve the initial state
    // return null if we're already in it
    public GameState resetState()
    {
        Debug.Assert(initialState != null, "GameStateController.resetState: initial state not set");

        // don't save the initial state multiple times in a row
        if (currentState == initialState) { return null; }

        stateHistory.Push(currentState);
        currentState = initialState;
        return initialState;
    }
}
