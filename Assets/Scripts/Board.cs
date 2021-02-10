using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/* GLOSSARY:
*   - logical position
*       integer position relative to the Board
*   - cell position
*       integer position relative to the Grid
*   - local position (tilemap)
*       integer position relative to a Tilemap
*   - world position
*       absolute position in Unity game space (i.e. gameObject.transform.position)
*/

public class Board : MonoBehaviour
{
    // tilemap containing Piece GameObjects
    //  - these should be placed on top of a ground tile
    [SerializeField]
    Tilemap pieceTilemap = default;

    // tilemap containing ground tiles
    //  - these determine the spaces pieces can walk on and the size of the board
    [SerializeField]
    Tilemap groundTilemap = default;

    // tilemap containing solution spaces
    //  - these also count as "ground spaces" that you can walk on and determine the size of the board
    [SerializeField]
    Tilemap solutionTilemap = default;

    // grid for the board
    Grid grid;

    // bottom left corner of board in Grid coordinates
    Vector2Int cellOrigin;

    // dimensions of the board
    Vector2Int size;

    // pieces on the board (size.x by size.y array)
    Piece[,] pieces;

    // reverse mapping piece => position
    Dictionary<Piece, Vector2Int> positions;

    // hero piece
    Piece hero;

    // true if there is ground at a location, false if not (size.x by size.y array)
    bool[,] ground;

    // unit vector that represents the direction the top of the board is facing relative to the game
    Vector2Int orientation;

    // board history collector/retriever
    GameStateController stateController = new GameStateController();

    // set of solution positions on the board
    HashSet<Vector2Int> solutions;

    // whether the board has been solved
    bool solved;

    #region initialization

    void Awake()
    {
        // validate required fields
        Debug.Assert(pieceTilemap != null, "Board.Awake: piece tilemap not found");
        Debug.Assert(groundTilemap != null, "Board.Awake: ground tilemap not found");

        // get tile map
        grid = GetComponent<Grid>();
        Debug.Assert(grid != null, "Board.Awake: grid not found");

        // determine and set size
        determineBoardSize();

        // init members
        orientation = Vector2Int.up; //north
        pieces = new Piece[size.x, size.y];
        positions = new Dictionary<Piece, Vector2Int>();
        ground = new bool[size.x, size.y];
        solutions = new HashSet<Vector2Int>();

        // set ground and solution
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                
                if (hasTile(groundTilemap, position))
                {
                    ground[x, y] = true;
                }
                if (hasTile(solutionTilemap, position))
                {
                    ground[x, y] = true;
                    solutions.Add(position);
                }
            }
        }

        // find and register pieces
        Piece[] piecesToRegister = pieceTilemap.GetComponentsInChildren<Piece>();
        foreach (Piece piece in piecesToRegister)
        {
            registerPiece(piece);
        }

        // save initial state
        saveState();
    }

    // determines board size and cellOrigin (i.e. Grid coordinates)
    //  - this is determined by the bounds of each associated tilemap (except the pieceTilemap)
    //    the bounds of the board are the smallest bounds possible that contain all the tilemaps
    //    i.e. the right edge of the board is the furthest right edge of any of the tilemaps
    void determineBoardSize()
    {
        Tilemap[] tilemaps = new[] { groundTilemap, solutionTilemap };

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);  //bottom right corner
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);  //top left corner

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null) { continue; }

            // compress bounds - otherwise, the bounds might extend beyond any actual tiles
            tilemap.CompressBounds();

            Vector2Int tmMin = v2(tilemap.cellBounds.min);
            Vector2Int tmMax = v2(tilemap.cellBounds.max);

            min.x = Mathf.Min(min.x, tmMin.x);
            min.y = Mathf.Min(min.y, tmMin.y);

            max.x = Mathf.Max(max.x, tmMax.x);
            max.y = Mathf.Max(max.y ,tmMax.y);
        }

        cellOrigin = min;
        size = max - min;

        Debug.Assert(size.x > 0 && size.y > 0, "Board.determineBoardSize: invalid board size " + size);
    }

    // checks if a tilemap has a tile at the given logical position
    bool hasTile(Tilemap tilemap, Vector2Int logicalPosition)
    {
        if (tilemap == null) { return false; }

        // tilemap.HasTile accepts a Vector3Int cell position, so convert to that
        Vector3Int cellPosition = v3(logicalPosition + cellOrigin);
        return tilemap.HasTile(cellPosition);
    }

    // registers a piece on the board for the first time, setting it at its initial position
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

    #endregion

    #region input entry points

    // direction > 0 for counter-clockwise, direction < 0 for clockwise
    public void rotate(int direction)
    {
        if (direction == 0) { return; }

        orientation = rotateVector(orientation, direction);
        updateBoard();
    }

    // moves the hero piece in response to user input
    public void moveHero(Vector2Int direction)
    {
        direction = adjustVector(direction);
        if (tryMovePiece(hero, direction))
        {
            updateBoard();
        }
    }

    // undoes the previous move
    public void undo()
    {
        restoreState(stateController.undoState());
    }

    // resets to the original state
    public void reset()
    {
        restoreState(stateController.resetState());
    }

    #endregion

    #region board update/piece movement

    void Update()
    {
        if (isAnimating) { updateAnimation(); }
    }

    void updateBoard()
    {
        fall();
        checkSolution();
        saveState();
        updateWorldRotation();
    }

    // returns whether the move was successful (i.e. was not blocked)
    bool tryMovePiece(Piece piece, Vector2Int move)
    {
        Debug.Assert(piece != null, "Board.movePiece: passed null");

        Debug.Assert(move.x == 0 || move.y == 0, "Board.movePiece: cannot move diagonally");

        // may as well exit early
        if (move == Vector2Int.zero) { return true; }

        Vector2Int newPos = positions[piece] + move;

        // check if move lies inside board or is off ground
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

    void checkSolution()
    {
        // if we're already in the win state or there is no win state, do nothing
        if (solved || positions.Count == 0) { return; }

        foreach (Vector2Int position in solutions)
        {
            Piece piece = getPiece(position);
            if (piece == null || piece == hero)
            {
                return;
            }
        }

        // if we got here, the board is solved
        solved = true;
        Debug.Log("you win!!!!!!");
    }

    #endregion

    #region game state history

    // saves the current state to the stateController
    void saveState()
    {
        GameState state = new GameState();
        state.positions = new Dictionary<Piece, Vector2Int>(positions);  // copy positions
        state.orientation = orientation;
        state.solved = solved;
        
        stateController.saveState(state);
    }

    // restores a given state
    void restoreState(GameState state)
    {
        if (state == null) { return; }

        foreach (Piece piece in state.positions.Keys)
        {
            Vector2Int position = state.positions[piece];
            movePiece(piece, position);
        }

        orientation = state.orientation;
        updateWorldRotation(false);

        solved = state.solved;
    }

    #endregion

    #region animation

    // current rotation of board
    float currentAngle
    {
        get { return transform.rotation.eulerAngles.z; }
        set { transform.Rotate(0, 0, value - currentAngle); }
    }

    // target rotation (if not animating, this shoulkd equal currentAngle)
    float targetAngle;

    // num seconds the animation takes
    const float TIME_TO_ANIMATE = 0.5f;

    // whether the board rotation is curently animating
    bool isAnimating;

    // time when the animation started
    float animationStartTime;

    float speed;

    // turns the board's game object to match its logical direction
    // we animate by default
    void updateWorldRotation(bool animate = true)
    {
        targetAngle = Vector3.SignedAngle(Vector3.up, v3(orientation), Vector3.forward);

        if (animate)
        {
            startAnimation();
        }
        else
        {
            currentAngle = targetAngle;
        }
    }

    void startAnimation()
    {
        isAnimating = true;
        animationStartTime = Time.time;
        speed = (targetAngle - currentAngle) / TIME_TO_ANIMATE;
    }

    void updateAnimation()
    {
        if (Time.time > animationStartTime + TIME_TO_ANIMATE) { finishAnimation(); }

        currentAngle = currentAngle + speed * Time.deltaTime;
    }

    void finishAnimation()
    {
        isAnimating = false;
        currentAngle = targetAngle;
    }

    #endregion

    #region helpers

    // validates whether a position is on the board
    bool onBoard(Vector2Int position)
    {
        return 0 <= position.x && position.x < size.x && 0 <= position.y && position.y < size.y && ground[position.x, position.y];
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
        return grid.GetCellCenterWorld(v3(logicalPosition + cellOrigin));
    }

    // converts a world position to a logical position
    Vector2Int logFromWorld(Vector3 worldPosition)
    {
        return v2(grid.WorldToCell(worldPosition)) - cellOrigin;
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

    #endregion
}
