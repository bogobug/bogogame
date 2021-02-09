using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Board : MonoBehaviour
{
    // size of the board
    [SerializeField]
    Vector2Int size = new Vector2Int(8, 8);

    // background tiles
    [SerializeField]
    Tile tilePrefab = default;

    // background tiles
    Tile[,] tiles;

    // pieces
    Piece[,] pieces;

    // reverse mapping piece => position
    public Dictionary<Piece, Vector2Int> positions;

    // hero piece
    Piece hero;

    // unit vector that represents the direction the top of the board is facing relative to the game
    Vector2Int orientation;

    // board history collector/retriever
    GameStateController gameStateController = new GameStateController();


    void Awake()
    {
        orientation = Vector2Int.up; // north

        tiles = new Tile[size.x, size.y];

        for (int x=0; x < size.x; x++)
        {
            for (int y=0; y < size.y; y++)
            {
                createTile(x, y);
            }
        }

        pieces = new Piece[size.x, size.y];

        Piece[] pieceArr = GetComponentsInChildren<Piece>();
        positions = new Dictionary<Piece, Vector2Int>(pieceArr.Length);

        foreach (Piece piece in pieceArr)
        {
            registerPiece(piece);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            Debug.Log("Undo Pressed");
            orientation = gameStateController.undo(this, orientation);

        }
    }

    // creates a background tile during Awake()
    // these are used both for background graphics
    // and as markers to move pieces to
    void createTile(int x, int y)
    {
        // create tile and positions
        Tile tile = tiles[x, y] = Instantiate<Tile>(tilePrefab);

        // set transform
        tile.transform.SetParent(transform, false);
        tile.transform.localPosition = new Vector2(x - size.x/2, y - size.y/2);
    }

    // registers a piece on the board for the first time, setting it at its initial position
    // public so that dynamic created pieces can call it on themselves
    public void registerPiece(Piece piece)
    {
        if (piece.hero)
        {
            Debug.Assert(hero == null, "Board.registerPiece: cannot have more than one hero");
            hero = piece;
        }

        // move to tile
        setPiece(piece, piece.startPosition);
    }

    // positive direction is counter-clockwise (as god intended)
    public void rotate(int direction)
    {
        if (direction == 0) { return; }

        gameStateController.saveBoardState(positions, direction);

        orientation = rotateVector(orientation, direction);
        fall();
        animateRotate(direction);
    }

    // moves the hero piece in response to user input
    public void moveHero(Vector2Int direction)
    {
        gameStateController.saveBoardState(positions, 0);
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
    public void movePiece(Piece piece, Vector2Int position)
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

        // move game object
        Tile tile = tiles[position.x, position.y];
        piece.gameObject.transform.localPosition = tile.gameObject.transform.localPosition;
    }

    // all pieces fall down
    void fall()
    {
        Vector2Int fallVector = adjustVector(Vector2Int.down);

        Piece[] pieces = new Piece[positions.Count];
        positions.Keys.CopyTo(pieces, 0);

        foreach (Piece piece in pieces)
        {
            if (piece.gravity)
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
            if (otherPiece.gravity) { continue; }

            // non-falling piece => we're done
            break;
        }

        movePiece(piece, newPos);
    }

    // turns the board's game object to match its logical direction
    public void animateRotate(int direction)
    {
        transform.Rotate(0, 0, 90 * direction);
    }

    // validates whether a position is on the board
    bool onBoard(Vector2Int position)
    {
        return 0 <= position.x && position.x < size.x && 0 <= position.y && position.y < size.y;
    }

    // direction > 0 for counter-clockwise, direction < 0 for clockwise
    public static Vector2Int rotateVector(Vector2Int vector, int direction)
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

    // validate unity inspector size
    void OnValidate()
    {
        if (size.x < 2) { size.x = 2; }

        if (size.y < 2) { size.y = 2; }
    }
}
