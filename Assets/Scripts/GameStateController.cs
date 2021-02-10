using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameStateController 
{
    Stack<Dictionary<Piece, Vector2Int>> boardStates = new Stack<Dictionary<Piece, Vector2Int>>();
    Stack<Vector2Int> orientations = new Stack<Vector2Int>();
    Dictionary<Piece, Vector2Int> initialBoardState;
    bool lastActionReset = true;

    public void saveBoardState(Dictionary<Piece, Vector2Int> boardState, Vector2Int orientation, bool reset) 
    {
        Dictionary<Piece, Vector2Int> boardStateCopy= new Dictionary<Piece, Vector2Int>(boardState);
        if (boardState.Count == 0)
        {
            initialBoardState = boardStateCopy;
        }
        boardStates.Push(boardStateCopy);
        orientations.Push(orientation);
        Debug.Log("Save " + boardStates.Count);

        if (initialBoardState == null)
        {
            initialBoardState = new Dictionary<Piece, Vector2Int>(boardStateCopy);
        }

        lastActionReset = reset;
    }

    //undoes board state and returns new orientation to board
    public Vector2Int undo(Board board, Vector2Int orientation)
    {
        if (boardStates.Count != 0)
        {
            Dictionary<Piece, Vector2Int> stateToReturnTo = boardStates.Pop();
            Vector2Int priorOrientation = orientations.Pop();
            Debug.Log("Undo " + boardStates.Count);
            var pieces = stateToReturnTo.Keys;

            foreach (var piece in pieces)
            {
                Vector2Int lastPosition = stateToReturnTo[piece];
                board.movePiece(piece, lastPosition);
                Debug.Log("Loop" + piece);
            }

            correctOrientation(board, orientation, priorOrientation);

            return priorOrientation;
        }
        return orientation;
    }

    public Vector2Int reset(Board board, Dictionary<Piece, Vector2Int> currentBoardState, Vector2Int orientation)
    {
        if (lastActionReset)
        {
            return Vector2Int.up;
        }

        saveBoardState(currentBoardState, orientation, true);
        
        var pieces = initialBoardState.Keys;

        foreach (var piece in pieces)
        {
            Vector2Int lastPosition = initialBoardState[piece];
            board.movePiece(piece, lastPosition);
        }

        correctOrientation(board, orientation, Vector2Int.up);

        return Vector2Int.up;
    }

    void correctOrientation(Board board, Vector2Int currentOrientation, Vector2Int goalOrientation)
    {
        Vector2 currentOrientationVector = new Vector2(currentOrientation.x, currentOrientation.y);
        Vector2 goalOrientationVector = new Vector2(goalOrientation.x, goalOrientation.y);

        float rotationRequired = Vector2.SignedAngle(currentOrientationVector, goalOrientationVector);


        if (rotationRequired == 90.0)
        {
            board.animateRotate(1);
        }

        else if (rotationRequired == -90.0)
        {
            board.animateRotate(-1);
        }

        else if ((rotationRequired == 180.0) || (rotationRequired == -180.0))
        {
            board.animateRotate(1);
            board.animateRotate(1);
        }
    }
}
