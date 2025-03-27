using UnityEngine;
using UnityEngine.InputSystem;

public class MuteOn : MonoBehaviour
{
    [SerializeField]
    private string actionName = "Attack";
    InputAction action;
    AudioSource audioSource;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        action = InputSystem.actions.FindAction(actionName);

        audioSource = GetComponent<AudioSource>();

        action.performed += ctx => Unmute();
        action.canceled += ctx => Mute();

    }

    void Mute() {
        Debug.Log("Mute");
        audioSource.mute = true;
    }

    void Unmute() {
        Debug.Log("Unmute");
        audioSource.mute = false;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
