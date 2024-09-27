using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
using UnityEditor;
using System.Diagnostics;

public class MGPT_InputPrompts : MonoBehaviour
{
    public TMP_InputField promptInputField;
    public TMP_Text responseText;
    public TMP_Text prompts;
    public Button sendButton;
    public GameObject model;
    private string prompt;
    public float moveSpeed = 0.3f;


    private string downloadUrl = "http://localhost:8000/download_fbx/?filename=bvh_0_out.fbx";
    private string savePath = "Assets/DownloadedAnimations/";
    private string fbxFileName = "bvh_0_out.fbx";
    private string gen_text2motion = "http://localhost:8000/gen_text2motion/";
    // Set up the URL for the POST request
    private string input_prompts = "http://localhost:8000/input_prompts/";
    private bool isProcessing = false;
    private Stopwatch stopwatch;
    // Define the target position
    private Vector3 targetPosition = new Vector3(-0.5f, 0, 1f);
    // Speed of movement


    void Start()
    {
        responseText.text = "";
        prompts.text = "";
        stopwatch = new Stopwatch();
        // Add a listener to check the input field's text every time it changes
        promptInputField.onValueChanged.AddListener(OnInputFieldChanged);

        // Check initial state of input field
        OnInputFieldChanged(promptInputField.text);
        sendButton.onClick.AddListener(OnSendButtonClicked);


    }

    void OnInputFieldChanged(string text)
    {
        // Enable the send button only if the input field is not empty
        sendButton.interactable = !string.IsNullOrEmpty(text);
    }
    void OnSendButtonClicked()
    {
        if (!isProcessing)
        {
            if (model != null)
            {
                model.SetActive(false); // Disable the model initially
            }
            StartCoroutine(ProcessSequence());
        }
    }


    IEnumerator ProcessSequence()
    {
        isProcessing = true;
        sendButton.interactable = false; // Disable the button

        // Send prompt
        prompt = promptInputField.text;
        UnityEngine.Debug.Log("Your Input Prompts: " + prompt);
        prompts.text = "Your Input Prompts: " + prompt;

        // Send the prompt to the backend before generating the motion
        yield return StartCoroutine(SendPromptToBackend(prompt));


        // Call run_both API
        yield return StartCoroutine(CallGen_Text2Motion_Endpoint());

        // Download FBX file
        yield return StartCoroutine(DownloadFBXFile(downloadUrl, savePath, fbxFileName));

        // Set the model's position
        if (model != null)
        {
            model.transform.position = targetPosition;
            model.SetActive(true); // Enable the model
        }

        EndProcessing(true); // Stop the stopwatch and update the UI with success message

        isProcessing = false;
        sendButton.interactable = true; // Enable the button
    }

    IEnumerator CallGen_Text2Motion_Endpoint()
    {
        string cfg = "./configs/config_h3d_stage3.yaml";
        string input_file = "./input.txt";


        // Construct the URL with parameters
        string url = $"{gen_text2motion}?cfg={cfg}&example={input_file}";

        UnityWebRequest request = UnityWebRequest.Get(url);
        responseText.text = "Status: Processing...";
        StartProcessing(); // Start the stopwatch

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            UnityEngine.Debug.LogError("Error calling run_both API: " + request.error);
        }
        else
        {
            UnityEngine.Debug.Log("run_both API call successful. Response: " + request.downloadHandler.text);
        }
    }
    IEnumerator SendPromptToBackend(string prompt)
    {


        // Create a JSON object for the prompt
        string jsonData = "{\"prompt\": \"" + prompt + "\"}";

        // Create a UnityWebRequest to send the JSON data
        UnityWebRequest request = new UnityWebRequest(input_prompts, "POST");
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(jsonToSend);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // Send the request and wait for a response
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            UnityEngine.Debug.LogError("Error sending prompt: " + request.error);
        }
        else
        {
            UnityEngine.Debug.Log("Prompt successfully sent to backend. Response: " + request.downloadHandler.text);
        }
    }


    IEnumerator DownloadFBXFile(string url, string path, string fileName)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            UnityEngine.Debug.LogError("Error downloading FBX file: " + request.error);
        }
        else
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            string filePath = Path.Combine(path, fileName);
            File.WriteAllBytes(filePath, request.downloadHandler.data);
            UnityEngine.Debug.Log("FBX file downloaded and saved to: " + filePath);

            // Refresh the AssetDatabase (only in Editor)
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
    }

    // Call this method when you start processing
    public void StartProcessing()
    {
        stopwatch.Reset();
        stopwatch.Start();
    }

    // Call this method when processing ends
    // 'successful' parameter to determine the final status message
    public void EndProcessing(bool successful)
    {
        stopwatch.Stop();
        UpdateProcessingTimeUI(successful);
    }

    // Updates the UI Text with the current processing time
    private void UpdateProcessingTimeUI(bool successful = false)
    {
        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        if (successful)
        {
            responseText.text = $"Status: Animation downloaded successfully! Time taken: {elapsedSeconds:F2} seconds";
        }
        else
        {
            responseText.text = $"Processing Time: {elapsedSeconds:F2} sec. / Est. 75 seconds";
        }
    }

    void Update()
    {
        if (isProcessing)
        {
            UpdateProcessingTimeUI(); // Continuously update UI while processing
        }
        // Check for arrow key inputs and move the model accordingly
        if (model != null && model.activeSelf)
        {
            Vector3 movement = Vector3.zero;

            if (Input.GetKey(KeyCode.UpArrow))
            {
                movement += Vector3.forward;
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                movement += Vector3.back;
            }
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                movement += Vector3.left;
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                movement += Vector3.right;
            }

            model.transform.position += movement * moveSpeed * Time.deltaTime;
        }
    }
}
