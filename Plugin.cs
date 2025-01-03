using BepInEx;
using UnityEngine;
using System.Collections;

namespace SSea_Translation_PofC;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Create GameObject and add TextReplacer component
        GameObject textReplacerObj = new GameObject("TextReplacer");
        TextReplacementUtility replacer = textReplacerObj.AddComponent<TextReplacementUtility>();

        // export text every second for testing purposes
        StartCoroutine(ExportTextRoutine(replacer));
    }

    private IEnumerator ExportTextRoutine(TextReplacementUtility replacer)
    {
        while (true)
        {
            replacer.ExportCurrentText();
            yield return new WaitForSeconds(1f);
        }
    }
}
