using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameStateController 
{
    Stack<Dictionary<Piece, Vector2Int>> boardStates = new Stack<Dictionary<Piece, Vector2Int>>();
    Stack<int> directions = new Stack<int>();

    public void saveBoardState(Dictionary<Piece, Vector2Int> boardState, int direction) 
    {
        Dictionary<Piece, Vector2Int> boardStateCopy= new Dictionary<Piece, Vector2Int>(boardState);
        boardStates.Push(boardStateCopy);
        directions.Push(direction);
        Debug.Log("Save " + boardStates.Count);
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
}
