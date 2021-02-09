using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

public class GameStateController 
{
    Stack<Dictionary<Piece, Vector2Int>> boardStates = new Stack<Dictionary<Piece, Vector2Int>>();
    Stack<int> directions = new Stack<int>();
    Dictionary<Piece, Vector2Int> initialBoardState;
    bool lastStateReset = true;

    public void saveBoardState(Dictionary<Piece, Vector2Int> boardState, int direction) 
    {
        Dictionary<Piece, Vector2Int> boardStateCopy= new Dictionary<Piece, Vector2Int>(boardState);
        if (boardState.Count == 0)
        {
            initialBoardState = boardStateCopy;
        }
        boardStates.Push(boardStateCopy);
        directions.Push(direction);
        Debug.Log("Save " + boardStates.Count);
        lastStateReset = false;

        if (initialBoardState == null)
        {
            initialBoardState = new Dictionary<Piece, Vector2Int>(boardStateCopy);
        }
    }

    //undoes board state and returns new orientation to board
    public Vector2Int undo(Board board, Vector2Int orientation)
    {
        if (boardStates.Count != 0)
        {
            Dictionary<Piece, Vector2Int> stateToReturnTo = boardStates.Pop();
            int lastDirection = directions.Pop();
            Debug.Log("Undo " + boardStates.Count);
            var pieces = stateToReturnTo.Keys;

            foreach (var piece in pieces)
            {
                Vector2Int lastPosition = stateToReturnTo[piece];
                board.movePiece(piece, lastPosition);
            }

            if (lastDirection != 0)
            {
                board.animateRotate(-lastDirection);
                return Board.rotateVector(orientation, -lastDirection);
            }
        }

        return orientation;
    }

    public void reset(Board board, Dictionary<Piece, Vector2Int> currentBoardState, Vector2Int orientation)
    {
        var pieces = initialBoardState.Keys;

        foreach (var piece in pieces)
        {
            Vector2Int lastPosition = initialBoardState[piece];
            board.movePiece(piece, lastPosition);
        }

        if (orientation == Vector2Int.down)
        {
            board.animateRotate(1);
            board.animateRotate(1);
            Board.rotateVector(orientation, 1);
            Board.rotateVector(orientation, 1);
        } 
            
        else if (orientation == Vector2Int.left)
        {
            board.animateRotate(1);
            Board.rotateVector(orientation, 1);
        }

        else if (orientation == Vector2Int.right)
        {
            board.animateRotate(-1);
            Board.rotateVector(orientation, -1);
        }

        if (lastStateReset == false)
        {
            saveBoardState(initialBoardState, 0);
            lastStateReset = true;
        }
            
        
    }
}
