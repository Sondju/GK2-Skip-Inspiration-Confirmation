using BepInEx;
using GK2.Framework;
using SkipInspirationConfirmation;

namespace SkipInspirationConfirmationFramework
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(MainPlugin.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ru.superman4eg.gk2.framework", BepInDependency.DependencyFlags.HardDependency)] // Твоя проверенная зависимость!
    public sealed class FrameworkBridgePlugin : BaseUnityPlugin
    {
        // Уникальный GUID моста, чтобы разорвать любые петли сортировки BepInEx
        public const string PluginGuid = "com.sondju.gk2.SkipInspirationConfirmation.framework";
        public const string PluginName = "Skip Inspiration Confirmation";
        public const string PluginVersion = "1.4.0";

        private void Awake()
        {
            MainPlugin main = MainPlugin.Instance;
            if (main == null)
            {
                Logger.LogError("Main mod instance is unavailable.");
                return;
            }

            // Регистрируем мост во фреймворке, скармливая ему main.Config по официальному гайду
            FrameworkApi.RegisterMod(new FrameworkBridge(main), main.Config);
            Logger.LogInfo("Skip Inspiration Confirmation - Framework Integration Bridge loaded successfully!");
        }

        private sealed class FrameworkBridge : Gk2ModBase
        {
            private readonly MainPlugin main;

            // Выставляем frameworkManagesEnabledState: false, чтобы тумблер в меню управлял оригинальным .cfg файлом
            private readonly Gk2ModMetadata metadata = new Gk2ModMetadata(
                PluginGuid, // Передаем GUID моста
                PluginName,
                "sondju",
                PluginVersion,
                "Optional GK2 Mod Framework integration for Skip Inspiration Confirmation.",
                supportsRuntimeToggle: true,
                requiresKnownBuild: false,
                frameworkManagesEnabledState: false);

            internal FrameworkBridge(MainPlugin main) { this.main = main; }
            public override Gk2ModMetadata Metadata => metadata;
            public override System.Collections.Generic.IReadOnlyList<Gk2ModDependency> Dependencies => null;

            public override void OnRegister(Gk2ModContext context)
            {
                // Регистрируем ТУ ЖЕ САМУЮ секцию и ключ, чтобы фреймворк связал меню с конфигом автономного мода
                context.Settings.AddToggle(
                    "General",
                    "ModEnabled",
                    true,
                    "Enable Mod",
                    "Completely disables confirmation windows when buying inspirations.");

                context.Settings.AddReadOnly(
                    "Status",
                    "Integration",
                    "Framework Integration",
                    "Shows whether the optional bridge is active.",
                    () => main != null ? "Active" : "Unavailable");
            }

            public override void OnEnable() { }
            public override void OnDisable() { }
            public override void OnGameStarted() { }
            public override void OnReturnedToMainMenu() { }
        }
    }
}
