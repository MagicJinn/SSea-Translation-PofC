using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;


namespace SSea_Translation_PofC;

public class TextReplacementUtility : MonoBehaviour
{
    [System.Serializable]
    public class TranslationEntry
    {
        public string originalText;
        public string translatedText;
    }

    [Header("Configuration")]
    [SerializeField]
    private bool includeInactive = true;
    [SerializeField]
    private bool persistentObject = true;
    [SerializeField]
    private float checkInterval = 1f; // How often to check for new text (in seconds)

    private float lastCheckTime = 0f;

    private static TextReplacementUtility instance;
    private HashSet<int> translatedObjects = new HashSet<int>();
    private bool isCurrentlyTranslating = false;

    void Awake()
    {
        if (persistentObject)
        {
            if (instance != null)
            {
                Destroy(this.gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(this.gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }


    private IEnumerator TranslateText(Text textComponent, string text, Action<bool> onComplete)
    {
        Debug.Log($"Starting translation request for text: {text}");
        using (var request = new UnityWebRequest("http://127.0.0.1:5000/translate", "POST"))
        {
            var jsonData = JsonUtility.ToJson(new TranslationRequest
            {
                q = text,
                source = "en",
                target = "ru"
            });

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.Send();

            if (!request.isError && textComponent != null)
            {
                try
                {
                    var result = JsonUtility.FromJson<TranslationResponse>(request.downloadHandler.text);
                    Debug.Log($"Successfully translated: '{text}' -> '{result.translatedText}'");

                    // Check if the text component is still valid and active
                    if (textComponent != null && textComponent.gameObject != null && textComponent.gameObject.activeInHierarchy)
                    {
                        textComponent.text = result.translatedText;
                        onComplete(true);
                    }
                    else
                    {
                        Debug.Log("Text component was destroyed or deactivated during translation");
                        onComplete(false);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Translation parsing error: {e.Message}\nResponse was: {request.downloadHandler.text}");
                    onComplete(false);
                }
            }
            else
            {
                Debug.LogError($"Translation request failed: {request.error}\nURL: {request.url}\nResponse Code: {request.responseCode}");
                onComplete(false);
            }
        }
    }

    [System.Serializable]
    private class TranslationRequest
    {
        public string q;
        public string source;
        public string target;
    }

    [System.Serializable]
    private class TranslationResponse
    {
        public string translatedText = string.Empty;
    }

    private IEnumerator ReplaceAllTextCoroutine()
    {
        if (!Application.isPlaying) yield break;

        isCurrentlyTranslating = true;

        var textComponents = FindObjectsOfType<Text>()
            .Where(t => (t.gameObject.activeInHierarchy || includeInactive) &&
                        !translatedObjects.Contains(t.gameObject.GetInstanceID()))
            .ToArray();

        if (textComponents.Length == 0)
        {
            isCurrentlyTranslating = false;
            yield break;
        }

        foreach (var text in textComponents)
        {
            if (text == null || !text.gameObject ||
                string.IsNullOrEmpty(text.text) ||
                text.text.Trim().Length == 0 ||
                text.text.All(char.IsDigit) ||
                !text.text.Any(char.IsLetter))
            {
                if (text != null && text.gameObject != null)
                {
                    translatedObjects.Add(text.gameObject.GetInstanceID());
                }
                continue;
            }

            yield return new WaitForSeconds(0.5f);

            bool translationComplete = false;
            string originalText = text.text;

            // Pass the Text component reference to TranslateText
            yield return StartCoroutine(TranslateText(text, originalText, (success) =>
            {
                translationComplete = true;
                if (success && text != null && text.gameObject != null)
                {
                    translatedObjects.Add(text.gameObject.GetInstanceID());
                }
            }));

            while (!translationComplete) yield return null;
        }

        isCurrentlyTranslating = false;
    }

    public void ReplaceAllText()
    {
        StartCoroutine(ReplaceAllTextCoroutine());
    }

    // Add this method to reset translations (useful when loading new scenes)
    public void ResetTranslations()
    {
        translatedObjects.Clear();
    }

    // Optional: Add this to your OnSceneLoaded method
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Plugin.Logger.LogWarning("We are starting");
        ResetTranslations(); // Clear the tracked objects when loading a new scene
        ReplaceAllText();
    }

    // Helper method to export current text to JSON format
    public void ExportCurrentText()
    {
        var textComponents = FindObjectsOfType<Text>().Where(t => t.gameObject.activeInHierarchy || includeInactive).ToArray();
        var uniqueTexts = new HashSet<string>();
        var serializer = new JsonFx.Json.JsonWriter();

        // Load existing translations if they exist
        Dictionary<string, TranslationEntry[]> existingData = new Dictionary<string, TranslationEntry[]>();
        string exportPath = "exported_texts.json";
        if (File.Exists(exportPath))
        {
            try
            {
                string existingJson = File.ReadAllText(exportPath);
                var reader = new JsonFx.Json.JsonReader();
                existingData = reader.Read<Dictionary<string, TranslationEntry[]>>(existingJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading existing export file: {e.Message}");
            }
        }

        // Collect current texts
        foreach (var text in textComponents)
        {
            if (string.IsNullOrEmpty(text.text) || text.text.Trim().Length == 0 ||
                text.text.All(char.IsDigit) || !text.text.Any(char.IsLetter))
            {
                continue;
            }
            uniqueTexts.Add(text.text);
        }

        // Merge with existing entries
        var existingEntries = existingData.ContainsKey("entries") ?
            existingData["entries"].ToDictionary(e => e.originalText, e => e.translatedText) :
            new Dictionary<string, string>();

        // Create merged entries array
        var mergedEntries = uniqueTexts
            .Select(text => new TranslationEntry
            {
                originalText = text,
                translatedText = existingEntries.ContainsKey(text) ? existingEntries[text] : text
            })
            .Concat(existingData.ContainsKey("entries") ?
                existingData["entries"].Where(e => !uniqueTexts.Contains(e.originalText)) :
                new TranslationEntry[0])
            .ToArray();

        var exportData = new Dictionary<string, TranslationEntry[]>
        {
            ["entries"] = mergedEntries
        };

        string json = serializer.Write(exportData);

        File.WriteAllText(exportPath, json);

        Debug.Log($"Exported {mergedEntries.Length} unique texts (including {uniqueTexts.Count} new entries) to: {Path.GetFullPath(exportPath)}");
    }

    void Update()
    {
        // Only check for new text periodically instead of every frame
        if (!isCurrentlyTranslating && Time.time - lastCheckTime >= checkInterval)
        {
            lastCheckTime = Time.time;
            StartCoroutine(ReplaceAllTextCoroutine());
        }
    }
}