using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;


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
    private string translationFilePath = "translations.json";
    [SerializeField]
    private bool includeInactive = true;
    [SerializeField]
    private bool persistentObject = true;

    private Dictionary<string, string> translationDict;
    private static TextReplacementUtility instance;

    void Awake()
    {
        if (persistentObject)
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        LoadTranslations();
    }

    void LoadTranslations()
    {
        translationDict = new Dictionary<string, string>();
        string fullPath = Path.Combine(Application.persistentDataPath, translationFilePath);

        if (File.Exists(fullPath))
        {
            string jsonContent = File.ReadAllText(fullPath);
            try
            {
                var serializer = new JsonFx.Json.JsonReader();
                var jsonData = serializer.Read<Dictionary<string, TranslationEntry[]>>(jsonContent);
                var entries = jsonData["entries"];

                foreach (var entry in entries)
                {
                    translationDict[entry.originalText] = entry.translatedText;
                }
                Debug.Log($"Loaded {translationDict.Count} translations");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing translation file: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"Translation file not found at: {fullPath}");
        }
    }

    public void ReplaceAllText()
    {
        if (translationDict == null || translationDict.Count == 0)
        {
            Debug.LogError("No translations loaded!");
            return;
        }

        // Find all text components in the scene
        var textComponents = FindObjectsOfType<Text>().Where(t => t.gameObject.activeInHierarchy || includeInactive).ToArray();
        // var tmpComponents = FindObjectsOfType<TextMeshProUGUI>(includeInactive);
        // var tmpTextComponents = FindObjectsOfType<TextMeshPro>(includeInactive);

        int replacementCount = 0;

        // Replace Unity UI Text components
        foreach (var text in textComponents)
        {
            // Check if the text is empty, contains only numbers, or has no letters
            if (string.IsNullOrEmpty(text.text) || text.text.Trim().Length == 0 || text.text.All(char.IsDigit) || !text.text.Any(char.IsLetter))
            {
                continue; // Skip this text
            }

            if (translationDict.TryGetValue(text.text, out string translation))
            {
                text.text = translation;
                replacementCount++;
            }
        }

        Debug.Log($"Replaced {replacementCount} text elements");
    }

    // Optional: Call this when loading a new scene
    public void OnSceneLoaded()
    {
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
        // Run ReplaceAllText() every second
        if (Time.time % 1f < Time.deltaTime)
        {
            ReplaceAllText();
        }
    }
}