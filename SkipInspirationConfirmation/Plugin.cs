using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;

namespace SkipInspirationConfirmation
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MainPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sondju.gk2.SkipInspirationConfirmation";
        public const string PluginName = "Skip Inspiration Confirmation";
        public const string PluginVersion = "1.4.0";

        public static MainPlugin Instance { get; private set; }
        public static ManualLogSource Log;

        // Чистый конфиг BepInEx, который позже подхватит фреймворк
        public ConfigEntry<bool> ModEnabled { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ModEnabled = Config.Bind(
                "General", "ModEnabled", true,
                "Completely disables confirmation windows when buying inspirations."
            );

            // Если один патч отвалится, другие продолжат работу, вместо падения всего плагина
            var harmony = new Harmony(PluginGuid);

            var patchTypes = new[]
            {
                typeof(InspirationWidget_OnPress_Patch)
            };

            foreach (var pt in patchTypes)
            {
                try
                {
                    harmony.CreateClassProcessor(pt).Patch();
                    Logger.LogMessage($"{pt.Name} correct patched");
                }
                catch (Exception e)
                {
                    Logger.LogError(
                        $"{pt.Name} NOT patched: {e.GetType().Name}: {e.Message}\n" +
                        $"Stack trace: {e.StackTrace}"
                    );
                }
            }

            Log.LogInfo($"[{PluginName}] Standalone initialization finished successfully!");
        }

        // --- ПАТЧ МГНОВЕННОЙ ПОКУПКИ ВДОХНОВЕНИЯ (РАБОТАЕТ ВСЕГДА БЕЗ ОШИБОК КОМПИЛЯЦИИ) ---
        [HarmonyPatch(typeof(InspirationWidget), "OnPress")]
        public class InspirationWidget_OnPress_Patch
        {
            private static int _lastProcessedFrame = -1;

            [HarmonyPrefix]
            public static bool Prefix(InspirationWidget __instance)
            {
                if (__instance == null) return true;

                if (Instance == null || !Instance.ModEnabled.Value)
                {
                    return true;
                }

                // Защита от повторов многоуровневых карточек в один кадр
                if (Time.frameCount == _lastProcessedFrame)
                {
                    return false;
                }

                try
                {
                    _lastProcessedFrame = Time.frameCount;

                    if (__instance.IsBuyLocked) return true;

                    // Нативно используем найденное тобой публичное свойство
                    InspirationData inspData = __instance.InspirationData;

                    if (inspData != null)
                    {
                        if (!inspData.IsCompleted || inspData.isAllLevelsBought)
                        {
                            return true;
                        }

                        // Читаем защищенное поле data через Harmony-рефлексию
                        var dataField = AccessTools.Field(typeof(InspirationWidget), "data");
                        if (dataField != null && dataField.GetValue(__instance) is InspirationWidgetData widgetData)
                        {
                            Action purchaseAction = widgetData.OnPress;

                            if (purchaseAction != null)
                            {
                                Log.LogInfo($"[FreeInspirations] Smart Bypass: Instantly executing OnPress action from data on frame: {_lastProcessedFrame}");

                                // ИСПРАВЛЕНО: Вызываем приватный метод FinishFlyAndLock через рефлексию Harmony, убирая ошибку компиляции!
                                var parentWidget = __instance.GetComponentInParent<CharInspirationPageWidget>();
                                if (parentWidget != null)
                                {
                                    var flyMethod = AccessTools.Method(typeof(InspirationWidget), "FinishFlyAndLock", new Type[] { typeof(CharInspirationPageWidget), typeof(string) });
                                    flyMethod?.Invoke(__instance, new object[] { parentWidget, inspData.id });
                                }

                                // Вызываем оригинальный метод покупки
                                purchaseAction.Invoke();
                            }
                            else
                            {
                                Log.LogError("[FreeInspirations] Critical: Public property 'OnPress' in InspirationWidgetData was null!");
                                return true;
                            }
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"[FreeInspirations] Error in native click executor: {ex.Message}");
                    return true;
                }

                return false; // Полностью блокируем ванильное диалоговое окно подтверждения покупки
            }
        }
    }
}
