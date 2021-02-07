using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeroController : MonoBehaviour
{
    [SerializeField]
    Board board = default;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            board.moveHero(Vector2Int.up);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            board.moveHero(Vector2Int.down);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            board.moveHero(Vector2Int.left);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            board.moveHero(Vector2Int.right);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            board.rotate(1);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            board.rotate(-1);
        }
    }
}
