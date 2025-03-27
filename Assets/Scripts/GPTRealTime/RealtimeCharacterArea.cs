using UnityEngine;

namespace GptRealtime
{
    public class RealtimeCharacterArea : MonoBehaviour
    {
        [SerializeField] private GPTRealtime gPTRealTime;

        [SerializeField] private Color onColor = Color.green;

        [SerializeField] private Color offColor = Color.red;

        private void Start()
        {
            // Set the color of the character to red.
            GetComponent<Renderer>().material.color = offColor;
        }

        void OnTriggerEnter(Collider other)
        {
            Debug.Log("Collision detected");
            // Check if collision is with a player.
            if (other.gameObject.tag == "Player")
            {
                gPTRealTime.Call();
            }
        }


        void OnTriggerExit(Collider other)
        {
            Debug.Log("Collision ended");
            // Check if collision is with a player.
            if (other.gameObject.tag == "Player")
            {
                gPTRealTime.HangUp();
            }
        }

        public void TurnOn()
        {
            // Change the color of the character to green.
            Debug.Log("Turned on");
            GetComponent<Renderer>().material.color = onColor;
            
        }

        public void TurnOff()
        {
            // Change the color of the character to red.
            Debug.Log("Turned off");
            GetComponent<Renderer>().material.color = offColor;
        }
    }

}