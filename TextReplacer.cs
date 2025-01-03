using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using JsonFx.Json;


namespace SSea_Translation_PofC;

public class TextReplacementUtility : MonoBehaviour
{
    [Serializable]
    public class TranslationEntry
    {
        public string originalText;
        public string translatedText;
    }

    private const string translationFilePath = "translations.json";
    private const string exportedtextsFilePath = "exported_texts.json";
    private readonly JsonReader jsonReader = new JsonReader();
    private readonly JsonWriter jsonWriter = new JsonWriter();

    private Dictionary<string, string> translationDict = new();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        LoadTranslations();
    }

    void LoadTranslations() // Load translations from the translations.json file
    {
        if (File.Exists(translationFilePath))
        {
            string jsonContent = File.ReadAllText(translationFilePath);
            try
            {
                var entries = jsonReader.Read<TranslationEntry[]>(jsonContent);

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
        else Debug.LogError($"Translation file not found at: {translationFilePath}");
    }

    public void ReplaceAllText()
    {
        if (translationDict == null || translationDict.Count == 0)
        {
            Debug.LogError("No translations loaded!");
            return;
        }

        var textComponents = FindObjectsOfType<Text>()
            .Where(t => t.gameObject.activeInHierarchy)
                .ToArray(); // Find all active Text components in the scene (TMPro is not used in SSea)

        foreach (var text in textComponents)
        {
            if (string.IsNullOrEmpty(text.text) || text.text.Trim().Length == 0 ||
                text.text.All(char.IsDigit) || !text.text.Any(char.IsLetter))
            {
                continue; // Skip any empty strings, or strings that do not contain any letters (such as 4/10, " " or "123")
            }

            // If strings contain numbers, replace them with placeholders
            // Examples include: "You've lost {n1} x Echo (new total {n2})."
            string pattern = text.text;
            var numbers = System.Text.RegularExpressions.Regex.Matches(text.text, @"\d+")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .ToArray();

            for (int i = 0; i < numbers.Length; i++)
            {
                pattern = pattern.Replace(numbers[i], $"{{n{i + 1}}}");
            }

            if (translationDict.TryGetValue(pattern, out string translationPattern))
            {
                // Replace {n1}, {n2}, etc. in translation with original numbers
                string finalTranslation = translationPattern;
                for (int i = 0; i < numbers.Length; i++)
                {
                    finalTranslation = finalTranslation.Replace($"{{n{i + 1}}}", numbers[i]);
                }

                text.text = finalTranslation;
            }
            else if (translationDict.TryGetValue(text.text, out string directTranslation))
            {
                text.text = directTranslation;
            }
        }
    }

    // Optional: Call this when loading a new scene
    public void OnSceneLoaded()
    {
        // ReplaceAllText();
    }

    // Helper method to export current text to JSON format
    public void ExportCurrentText()
    {
        var textComponents = FindObjectsOfType<Text>().Where(t => t.gameObject.activeInHierarchy).ToArray();
        // Load existing translations if they exist
        TranslationEntry[] existingData = [];
        string exportPath = exportedtextsFilePath;

        var existingEntries = new Dictionary<string, string>();

        if (File.Exists(exportPath))
        {
            try
            {
                string existingJson = File.ReadAllText(exportPath);
                existingData = jsonReader.Read<TranslationEntry[]>(existingJson);

                foreach (var entry in existingData)
                {
                    existingEntries[entry.originalText] = entry.translatedText;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading existing export file: {e.Message}");
            }
        }

        // Helper function to convert numbers to placeholders
        Dictionary<string, List<string>> patternGroups = new Dictionary<string, List<string>>();

        foreach (var text in textComponents)
        {
            if (string.IsNullOrEmpty(text.text) || text.text.Trim().Length == 0 ||
                text.text.All(char.IsDigit) || !text.text.Any(char.IsLetter))
            {
                continue; // Skip any empty strings, or strings that do not contain any letters
            }

            // Create pattern by replacing numbers with {n1}, {n2}, etc.
            string pattern = text.text;
            var numbers = System.Text.RegularExpressions.Regex.Matches(text.text, @"\d+")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .ToArray();

            for (int i = 0; i < numbers.Length; i++)
            {
                pattern = pattern.Replace(numbers[i], $"{{n{i + 1}}}");
            }

            if (!patternGroups.ContainsKey(pattern))
            {
                patternGroups[pattern] = new List<string>();
            }
            patternGroups[pattern].Add(text.text);
        }

        // Create merged entries array, using patterns when applicable
        var mergedEntries = patternGroups
            .SelectMany(group =>
            {
                // If we have multiple entries that match the same pattern, use the pattern as the text (may or may not work)
                if (group.Value.Count > 1)
                {
                    return new TranslationEntry[] { new TranslationEntry
                    {
                        originalText = group.Key,
                        translatedText = existingEntries.ContainsKey(group.Key) ?
                            existingEntries[group.Key] : group.Key
                    }};
                }
                // Otherwise, use the original text
                return group.Value.Select(text => new TranslationEntry
                {
                    originalText = text,
                    translatedText = existingEntries.ContainsKey(text) ?
                        existingEntries[text] : text
                }).ToArray();
            })
            .Concat(existingData.Where(e => !patternGroups.Any(g => g.Value.Contains(e.originalText))))
            .ToArray();

        string json = jsonWriter.Write(mergedEntries);
        File.WriteAllText(exportPath, json);

        Debug.Log($"Exported {mergedEntries.Length} unique texts to: {Path.GetFullPath(exportPath)}");
    }

    void Update()
    {
        ReplaceAllText();
    }
}