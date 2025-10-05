using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerScript : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    public int money = 5000;

    Rigidbody2D rb;
    Vector2 input;

    public TextMeshProUGUI moneyText;

    SpriteRenderer SR;
    string prevMovingState = "idle";
    string movingState = "idle";
    int spriteAltStage = 0;
    [SerializeField] float spriteAltRate = 0.5f;
    float _tSpriteAlt = 0f;

    [SerializeField]
    [Header("Left Sprites")]
    private Sprite[] leftSprites = new Sprite[2];

    [SerializeField]
    [Header("Right Sprites")]
    private Sprite[] rightSprites = new Sprite[2];

    [SerializeField]
    [Header("Front Sprites")]
    private Sprite[] frontSprites = new Sprite[2];

    [SerializeField]
    [Header("Back Sprites")]
    private Sprite[] backSprites = new Sprite[2];



    private Transform mainCam;  // Your player
    [SerializeField] private float smoothSpeed = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Transform>();

        SR = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        SR.sprite = frontSprites[0];
    }

    // Update is called once per frame
    void Update()
    {
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        input = input.normalized;

        UpdateSprite();

        MoveCamera();

        moneyText.text = money.ToString() + "$";
    }

    void FixedUpdate()
    {
        rb.linearVelocity = input * speed;
    }

    void MoveCamera()
    {
        Vector3 desiredPosition = transform.position;
        Vector3 smoothedPosition = Vector3.Lerp(mainCam.transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        mainCam.transform.position = new Vector3(smoothedPosition.x, smoothedPosition.y, mainCam.transform.position.z);
    }

    void UpdateSprite()
    {
        _tSpriteAlt += Time.deltaTime;

        if (rb.linearVelocityX != 0)
        {
            movingState = (rb.linearVelocityX < 0) ? "left" : "right";
        }
        else if (rb.linearVelocityY != 0)
        {
            movingState = (rb.linearVelocityY < 0) ? "down" : "up";
        }
        else
        {
            movingState = "idle";
        }

        if (movingState == prevMovingState && _tSpriteAlt < spriteAltRate)
        {
            return;
        }

        _tSpriteAlt = 0;

        if (movingState == "right")
        {
            SR.sprite = rightSprites[spriteAltStage];
            spriteAltStage = 1 - spriteAltStage;
        }
        if (movingState == "left")
        {
            SR.sprite = leftSprites[spriteAltStage];
            spriteAltStage = 1 - spriteAltStage;
        }
        if (movingState == "down")
        {
            SR.sprite = frontSprites[spriteAltStage];
            spriteAltStage = 1 - spriteAltStage;
        }
        if (movingState == "up")
        {
            SR.sprite = backSprites[spriteAltStage];
            spriteAltStage = 1 - spriteAltStage;
        }
        if (movingState == "idle")
        {
            SR.sprite = frontSprites[0];
        }

        prevMovingState = movingState;
    }
}
