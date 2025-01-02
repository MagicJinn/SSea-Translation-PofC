using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using System.Collections;

namespace SSea_Translation_PofC;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Create GameObject and add TextReplacer component
        GameObject textReplacerObj = new GameObject("TextReplacer");
        TextReplacementUtility replacer = textReplacerObj.AddComponent<TextReplacementUtility>();
        DontDestroyOnLoad(textReplacerObj);

        // Start the coroutine to export text every second
        StartCoroutine(ExportTextRoutine(replacer));
    }

    private System.Collections.IEnumerator ExportTextRoutine(TextReplacementUtility replacer)
    {
        while (true)
        {
            replacer.ExportCurrentText();
            yield return new WaitForSeconds(1f);
        }
    }
}
