using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Board : MonoBehaviour
{
    Vector2Int size;

    // pieces
    Piece[,] pieces;

    // reverse mapping piece => position
    Dictionary<Piece, Vector2Int> positions;

    // hero piece
    Piece hero;

    // unit vector that represents the direction the top of the board is facing relative to the game
    Vector2Int orientation;

    // tilemap for the board
    Tilemap tilemap;

    // bottom left corner of board in Grid coordinates
    Vector2Int cellOrigin;

    void Awake()
    {
        // get tile map
        tilemap = GetComponent<Tilemap>();
        Debug.Assert(tilemap != null, "Board.Awake: tilemap not found");

        // determine and set size
        tilemap.CompressBounds();
        size = v2(tilemap.size);          //save these or they will be lost
        cellOrigin = v2(tilemap.origin);  //when we call tilemap.ClearAllTiles

        // init members
        orientation = Vector2Int.up; //north
        pieces = new Piece[size.x, size.y];
        positions = new Dictionary<Piece, Vector2Int>();

        // clear tiles
        // -  we do this because the only items we care about in the tilemap
        //    are GameObjects we painted with the prefab brush
        //    any plain tiles are just there to determine the bounds of the board
        //    and can be discarded
        tilemap.ClearAllTiles();

        // find and register pieces
        Piece[] piecesToRegister = GetComponentsInChildren<Piece>();
        foreach (Piece piece in piecesToRegister)
        {
            registerPiece(piece);
        }
    }

    // registers a piece on the board for the first time, setting it at its initial position
    // public so that dynamic created pieces can call it on themselves
    void registerPiece(Piece piece)
    {
        // check if this piece is the hero
        if (piece.GetComponent<HeroController>() != null)
        {
            Debug.Assert(hero == null, "Board.registerPiece: cannot have more than one hero");
            hero = piece;
        }

        // get logical position
        var logPosition = logFromWorld(piece.GetComponent<Transform>().position);

        // move to tile
        setPiece(piece, logPosition);
    }

    // direction > 0 for counter-clockwise, direction < 0 for clockwise
    public void rotate(int direction)
    {
        if (direction == 0) { return; }

        orientation = rotateVector(orientation, direction);
        fall();
        animateRotate(direction);
    }

    // moves the hero piece in response to user input
    public void moveHero(Vector2Int direction)
    {
        direction = adjustVector(direction);
        tryMovePiece(hero, direction);
        fall();
    }

    // returns whether the move was successful (i.e. was not blocked)
    bool tryMovePiece(Piece piece, Vector2Int move)
    {
        Debug.Assert(piece != null, "Board.movePiece: passed null");

        Debug.Assert(move.x == 0 || move.y == 0, "Board.movePiece: cannot move diagonally");

        // may as well exit early
        if (move == Vector2Int.zero) { return true; }

        Vector2Int newPos = positions[piece] + move;

        // check if move lies inside board
        if (!onBoard(newPos))
        {
            return false;
        }

        // check if the space is occupied by a piece
        Piece otherPiece = getPiece(newPos);
        if (otherPiece != null && otherPiece.pushable)
        {
            Vector2Int unitMove = move * (1 / (int)move.magnitude); //normalize

            // try moving the piece
            if (!tryMovePiece(otherPiece, unitMove))
            {
                return false;
            }
        }
        else if (otherPiece != null)
        {
            // if the piece isn't pushable, we can't move
            return false;
        }

        movePiece(piece, newPos);
        return true;
    }

    // returns the piece at the position
    Piece getPiece(Vector2Int position)
    {
        Debug.Assert(onBoard(position),
            "Board.setPiece: position " + position + " lies outside board of size " + size);

        return pieces[position.x, position.y];
    }

    // moves the piece to the position
    // assumes the position is vacant
    void movePiece(Piece piece, Vector2Int position)
    {
        Vector2Int oldPos = positions[piece];
        pieces[oldPos.x, oldPos.y] = null;

        setPiece(piece, position);
    }

    // sets a piece at the position
    // does not clear out the previous position - use movePiece instead
    void setPiece(Piece piece, Vector2Int position)
    {
        Debug.Assert(piece != null, "Board.setPiece: passed null piece");

        Debug.Assert(onBoard(position),
            "Board.setPiece: position " + position + " lies outside board of size " + size);
        
        Debug.Assert(pieces[position.x, position.y] == null,
            "Board.setPiece: position " + position + " already populated");

        // update logical position
        pieces[position.x, position.y] = piece;
        positions[piece] = position;

        // update world position
        piece.GetComponent<Transform>().position = worldFromLog(position);
    }

    // all pieces fall down
    void fall()
    {
        Vector2Int fallVector = adjustVector(Vector2Int.down);

        Piece[] pieces = new Piece[positions.Count];
        positions.Keys.CopyTo(pieces, 0);

        foreach (Piece piece in pieces)
        {
            if (piece.pushable)
            {
                fallPiece(piece, fallVector);
            }
        }
    }

    // given piece falls down
    void fallPiece(Piece piece, Vector2Int fallVector)
    {
        Vector2Int newPos = positions[piece];

        for (Vector2Int pos = positions[piece]; onBoard(pos); pos += fallVector)
        {
            Piece otherPiece = getPiece(pos);
            
            // fall through empty space
            if (otherPiece == null) { newPos += fallVector; continue; }

            // cannot fall through (so no increment),
            // but since this piece might fall, we can keep looping
            if (otherPiece.pushable) { continue; }

            // non-falling piece => we're done
            break;
        }

        movePiece(piece, newPos);
    }

    // turns the board's game object to match its logical direction
    void animateRotate(int direction)
    {
        // rotate this board and all sibling boards/tilemaps
        var tilemaps = transform.parent.GetComponentsInChildren<Tilemap>();

        foreach (Tilemap tilemap in tilemaps)
        {
            tilemap.transform.Rotate(0, 0, 90 * direction);
        }
    }

    // validates whether a position is on the board
    bool onBoard(Vector2Int position)
    {
        return 0 <= position.x && position.x < size.x && 0 <= position.y && position.y < size.y;
    }

    // direction > 0 for counter-clockwise, direction < 0 for clockwise
    static Vector2Int rotateVector(Vector2Int vector, int direction)
    {
        if (direction == 0) { return vector; }

        direction = direction > 0 ? 1 : -1; //normalize
        return new Vector2Int(direction * -1 * vector.y, direction * vector.x);
    }

    // transforms a vector relative to the game (true north) to a vector relative to the rotated board
    Vector2Int adjustVector(Vector2Int vector)
    {
        Debug.Assert(orientation.magnitude == 1 && (orientation.x == 0 || orientation.y == 0),
            "Board.adjustVector: orientation " + orientation + " is not a directional unit");

        return new Vector2Int(
            orientation.y * vector.x - orientation.x * vector.y,
            orientation.x * vector.x + orientation.y * vector.y
            );
    }

    // converts a logical position to a world position
    Vector3 worldFromLog(Vector2Int logicalPosition)
    {
        return tilemap.GetCellCenterWorld(v3(logicalPosition + cellOrigin));
    }

    // converts a world position to a logical position
    Vector2Int logFromWorld(Vector3 worldPosition)
    {
        return v2(tilemap.WorldToCell(worldPosition)) - cellOrigin;
    }

    // convert a Vector3Int to a Vector2Int in the XY plane
    static Vector2Int v2(Vector3Int v3)
    {
        return new Vector2Int(v3.x, v3.y);
    }

    // convert a Vector2Int in the XY plane to a Vector3Int
    static Vector3Int v3(Vector2Int v2)
    {
        return new Vector3Int(v2.x, v2.y, 0);
    }

    // validate unity inspector size
    void OnValidate()
    {
        if (size.x < 2) { size.x = 2; }

        if (size.y < 2) { size.y = 2; }
    }
}
