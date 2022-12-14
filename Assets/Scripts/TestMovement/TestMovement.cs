using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class TestMovement : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] GameObject attackIndicator;
    [SerializeField] GameObject parryShield;
    [SerializeField] GameObject shadowClone;
    [SerializeField] Animator anim;

    [Header("Character Stats")]
    [SerializeField] int HP = 10;
    [SerializeField] float speed = 1f;
    [SerializeField] CharacterController charController;
    [SerializeField] float controllerDeadZone = 0.1f;

    [Header("Other")]
    [SerializeField] RhythmKeeper rhythmKeeper;
    [SerializeField] Transform rayCastStart;

    public string lastBeat;
    public int playerID;

    private Vector2 movement;
    private Vector3 aim;
    private float hitStunRemaining = 0;
    private float maxValidInputTime; //Used to see if the next move falls under the correct combo timing
    private float validInputTimer; //Tracks the elapsed time of the current beat
    private PlayerControls playerControls;
    private bool isAttacking; //Prevents the player from acting during an attack
    private bool canMove;
    private bool isLaunching;
    private bool isDodging;
    private bool isParrying;
    private bool canParry = true;
    public bool isGamepad;

    void Awake()
    {
        playerControls = new PlayerControls();
        charController = GetComponent<CharacterController>();
        rhythmKeeper = GameObject.FindObjectOfType<RhythmKeeper>();
    }

    private void OnEnable()
    {
        playerControls.Enable();
    }

    private void OnDisable()
    {
        playerControls.Disable();
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        movement = ctx.ReadValue<Vector2>();
    }
    public void OnLook(InputAction.CallbackContext ctx)
    {
        aim = ctx.ReadValue<Vector2>();
    }
    public void OnDeviceChange(PlayerInput pi)
    {
        isGamepad = pi.currentControlScheme.Equals("Gamepad") ? true : false;
    }

    public void Dodge(InputAction.CallbackContext ctx)
    {
        if (!isDodging)
        {
            anim.SetTrigger("Dodge");
            //isDodging = true;
        }
    }
    public void Attack1(InputAction.CallbackContext ctx)
    {
        float beatPerc = validInputTimer / maxValidInputTime * 100; //Calculate percentage of beat

        if (ctx.performed)
        {
            if (rhythmKeeper.timingKey == "Miss" && maxValidInputTime == 0) { anim.SetTrigger("Missed"); return; }

            anim.SetTrigger("Light");

            if (maxValidInputTime == 0) { lastBeat = rhythmKeeper.timingKey; } //Get timing of input
            if (beatPerc < rhythmKeeper.normalLeewayPerc) { anim.SetTrigger("Missed"); }
            else if (beatPerc >= rhythmKeeper.normalLeewayPerc && beatPerc < rhythmKeeper.perfectLeewayPerc) { lastBeat = "Early"; }
            else if (beatPerc >= rhythmKeeper.perfectLeewayPerc && beatPerc < 100) { lastBeat = "Perfect"; }
            else { lastBeat = "Early"; }

            return;
        }
    }
    public void Attack2(InputAction.CallbackContext ctx)
    {
        float beatPerc = validInputTimer / maxValidInputTime * 100; //Calculate percentage of beat

        if (ctx.performed)
        {
            if (rhythmKeeper.timingKey == "Miss" && maxValidInputTime == 0) { anim.SetTrigger("Missed"); return; }

            anim.SetTrigger("Heavy");

            if (maxValidInputTime == 0) { lastBeat = rhythmKeeper.timingKey; } //Get timing of input

            if (beatPerc < rhythmKeeper.normalLeewayPerc) { anim.SetTrigger("Missed"); }
            else if (beatPerc >= rhythmKeeper.normalLeewayPerc && beatPerc < rhythmKeeper.perfectLeewayPerc) { lastBeat = "Early"; }
            else if (beatPerc >= rhythmKeeper.perfectLeewayPerc && beatPerc < 100) { lastBeat = "Perfect"; }
            else { lastBeat = "Early"; }

            return;
        }
    }
    public void Parry(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && canParry)
        {
            isParrying = true;
            StartCoroutine(parryTiming());
            parryShield.SetActive(true);
            canParry = false;
        }
    }

    void Update()
    {
        if (hitStunRemaining > 0)
        {
            hitStunRemaining -= Time.deltaTime;
            return;
        }

        if (maxValidInputTime != 0) { validInputTimer += Time.deltaTime; }

        if (!isAttacking && !isParrying)
        {
            HandleInput();
            HandleMovement();
            HandleRotation();
            HandleAction();
        }
        else if (canMove)
        {
            HandleInput();
            HandleMovement();
        }
        else if (isLaunching)
        {
            charController.Move(aim * Time.deltaTime);
        }
    }

    //Combat-related functions
    public void StartAttack() { isAttacking = true; canMove = false; validInputTimer = 0; maxValidInputTime = 0; }
    public void CanCancelAttack() { isAttacking = false; isLaunching = false; canMove = false; }
    public void EndAttack() { isAttacking = false; isLaunching = false; canMove = false; validInputTimer = 0; maxValidInputTime = 0; aim = new Vector2(0, 0); }
    public void CanMove() { canMove = true; }
    public void LaunchPlayer(float units)
    {
        isLaunching = transform;
        Vector3 launchDirection = gameObject.transform.forward * -units;
        aim = launchDirection;
    }
    public void EndLaunch()
    {
        isLaunching = false;
    }
    public void BeatsForNextAttack(int numOfBeats) //Use eighth notes for calculations
    {
        maxValidInputTime = rhythmKeeper.beatLength / 2; //Get time of eighth notes
        maxValidInputTime *= numOfBeats; //Set maxValidInputTime to x eighth notes
        rhythmKeeper.SpawnArrow(maxValidInputTime, 0);
    }

    void HandleInput() { }
    void HandleAction() { }
    public void HandleMovement()
    {
        Vector3 move = new Vector3(movement.x, 0, movement.y);
        charController.Move(move * Time.deltaTime * speed);
    }

    public void HandleRotation()
    {
        if (isGamepad)
        {
            if (Mathf.Abs(aim.x) > controllerDeadZone || Mathf.Abs(aim.y) > controllerDeadZone)
            {
                Vector3 playerDirection = Vector3.right * aim.x + Vector3.forward * aim.y;

                if (playerDirection.sqrMagnitude > 0.0f)
                {
                    Quaternion newRotation = Quaternion.LookRotation(-playerDirection, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, newRotation, 360);
                }
            }
            else if (movement.x != 0 || movement.y != 0)
            {
                Vector3 newMovement = new Vector3(movement.x, 0, movement.y);
                Quaternion newRotation = Quaternion.LookRotation(-newMovement, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, newRotation, 360);
            }
        }
        else
        {
            foreach(GameObject obj in GameObject.FindGameObjectsWithTag("Player"))
            {
                if(obj != this.gameObject) 
                {
                    Vector3 away = gameObject.transform.position - obj.transform.position;
                    Quaternion awayRot = Quaternion.LookRotation(away);
                    transform.rotation = awayRot;
                }
            }
        }
    }


    //Taking Damage
    public void TakeDamage(int damage, float hitStun, float knockBack, Transform hitBoxTransform)
    {
        if (!isParrying)
        {
            anim.Play("Base Layer.Test_Idle");
            isLaunching = false;
            isAttacking = false;
            hitStunRemaining = hitStun;
            HP -= damage;
            Vector3 launchDir = gameObject.transform.position - hitBoxTransform.position;
            launchDir.y = 0;
            launchDir.Normalize();
            charController.Move(launchDir * knockBack);
            aim = new Vector2(0, 0);
        }
    }

    public void SpawnShadowClone(float _fadeSpeed)
    {
        GameObject _shadowClone = Instantiate(shadowClone, transform.position, transform.rotation);
        _shadowClone.AddComponent<FadeObject>();
        _shadowClone.GetComponent<FadeObject>().fadeSpeed = _fadeSpeed;
    }


    //Coroutines
    IEnumerator parryTiming()
    {
        yield return new WaitForSeconds(0.1f);
        isParrying = false;
        parryShield.SetActive(false);
        yield return new WaitForSeconds(0.9f);
        canParry = true;
    }
}
