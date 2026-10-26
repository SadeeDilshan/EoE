using UnityEngine;
using TMPro; // Needed for UI text

[RequireComponent(typeof(CharacterController))]
public class ARIEPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float crouchSpeed = 2.5f;
    public float gravity = -9.81f;

    [Header("Stealth / Crouch Settings")]
    public float normalHeight = 2.0f;
    public float crouchHeight = 1.0f;
    public bool isCrouching = false;

    [Header("Camera Look Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;
    private float xRotation = 0f;

    [Header("Recycling & Level 1 Puzzle")]
    public float interactRange = 3.5f;
    public int scrapCount = 0;
    public int energyCells = 0;
    public GameObject securityDoor; // Drag SecurityDoor here
    public TMP_Text hudText;        // Drag ScrapText here

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UpdateHUD();
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovementAndCrouch();
        HandleInteraction();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovementAndCrouch()
    {
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            controller.height = isCrouching ? crouchHeight : normalHeight;
        }

        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                // 1. Collect Scrap
                if (hit.collider.CompareTag("Scrap"))
                {
                    scrapCount++;
                    Destroy(hit.collider.gameObject);
                    UpdateHUD();
                }
                // 2. Use Recycling Machine (Requires 3 Scrap)
                else if (hit.collider.CompareTag("Recycler"))
                {
                    if (scrapCount >= 3)
                    {
                        scrapCount -= 3;
                        energyCells++;
                        UpdateHUD();

                        // Open the security door!
                        if (securityDoor != null)
                        {
                            securityDoor.SetActive(false);
                            Debug.Log("Door Powered Open using Recycled Energy!");
                        }
                    }
                    else
                    {
                        Debug.Log("Need at least 3 Scrap to recycle into an Energy Cell!");
                    }
                }
                // 3. Rescue the Sapling (End of Level 1)
                else if (hit.collider.CompareTag("Sapling"))
                {
                    Destroy(hit.collider.gameObject);
                    if (hudText != null)
                        hudText.text = "SAPLING SECURED! Proceeding to Level 2...";
                    Debug.Log("Level 1 Complete!");
                }
            }
        }
    }

    void UpdateHUD()
    {
        if (hudText != null)
        {
            hudText.text = $"Scrap: {scrapCount}/3 | Energy Cells: {energyCells}";
        }
    }
}