using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using MonoMod.Utils;
using Celeste.Mod.UI;

namespace Celeste.Mod.EvelynModToggleExtra
{
    public class EvelynModToggleExtraModule : EverestModule
    {
        public static EvelynModToggleExtraModule Instance { get; private set; }

        public override Type SettingsType => typeof(EvelynModToggleExtraModuleSettings);
        public static EvelynModToggleExtraModuleSettings Settings => (EvelynModToggleExtraModuleSettings)Instance._Settings;

        public static HashSet<string> Dependencies;
        public static string DependencyPath;

        public EvelynModToggleExtraModule()
        {
            Instance = this;
        }

        public override void Load()
        {
            On.Celeste.Mod.UI.OuiModToggler.addToBlacklist += onAddToBlacklist;
            On.Celeste.Mod.UI.OuiModToggler.addFileToMenu += onAddFileToMenu;
            On.Celeste.Mod.UI.OuiDependencyDownloader.downloadDependency += onDownloadDependency;
            LoadDependencyList();

        }

        public override void Unload()
        {
            On.Celeste.Mod.UI.OuiModToggler.addToBlacklist -= onAddToBlacklist;
            On.Celeste.Mod.UI.OuiModToggler.addFileToMenu -= onAddFileToMenu;
            On.Celeste.Mod.UI.OuiDependencyDownloader.downloadDependency -= onDownloadDependency;
        }

        public static void LoadDependencyList() {
            if (Settings.DepPath == null) Settings.DepPath = "dependencies.txt";
            DependencyPath = Path.Combine(Everest.Loader.PathMods, Settings.DepPath);
            Dependencies = new HashSet<string>();
            foreach (string line in File.ReadAllLines(DependencyPath)) {
                if (line.StartsWith("#")) continue;

                Dependencies.Add(line.Trim());
            }
        }

        public static void SaveDependencyList() {
            List<string> lines = new List<string>{ "# This is the dependency list. Lines starting with # are ignored.", "# File generated through the \"Toggle Dependencies\" menu in Mod Options" };
            foreach (string filepath in Directory.GetFiles(Everest.Loader.PathMods)) {
               string file = Path.GetFileName(filepath);

               lines.Add(Dependencies.Contains(file) ? file : "# " + file);
            }

            File.WriteAllLines(DependencyPath, lines);
        }

        public void onAddToBlacklist(On.Celeste.Mod.UI.OuiModToggler.orig_addToBlacklist orig, Mod.UI.OuiGenericMenu self, string file) {
            orig(self, file);

            DynamicData self_data = new DynamicData(self);
            var toggleDependencies = self_data.Get<bool>("toggleDependencies");
            var modYamls = self_data.Get<Dictionary<string, EverestModuleMetadata[]>>("modYamls");
            var modToggles = self_data.Get<Dictionary<string, TextMenu.OnOff>>("modToggles");
            var blacklistedMods = self_data.Get<HashSet<string>>("blacklistedMods");

            if (Settings.HideDep && toggleDependencies && modYamls.TryGetValue(file, out EverestModuleMetadata[] baseMetas)){
                foreach (KeyValuePair<string, EverestModuleMetadata[]> metadatas in modYamls) {
                    if (!Dependencies.Contains(metadatas.Key)) {
                        Logger.Log(LogLevel.Verbose, "EvelynModToggleExtra/addToBlacklist", "Skipping due to not being marked as dependency: " + metadatas.Key + " / " + file);
                        continue;
                    }

                    if (blacklistedMods.Contains(metadatas.Key)) {
                        Logger.Log(LogLevel.Verbose, "EvelynModToggleExtra/addToBlacklist", "Skipping due to already being blacklisted: " + metadatas.Key + " / " + file);
                    }

                    if (!metadatas.Value.Any(metadata => baseMetas.Any(baseMeta => baseMeta.Dependencies.Any(baseMetaDep => baseMetaDep.Name == metadata.Name)))) {
                        Logger.Log(LogLevel.Verbose, "EvelynModToggleExtra/addToBlacklist", "Skipping due to not being dependency of removing mod: " + metadatas.Key + " / " + file);
                        continue;
                    }

                    if (metadatas.Value.Any(metadata => modYamls.Any(yaml => !blacklistedMods.Contains(yaml.Key) && yaml.Key != metadatas.Key && yaml.Value.Any(otherMeta => otherMeta.Dependencies.Any(dep => dep.Name == metadata.Name))))) {
                        Logger.Log(LogLevel.Verbose, "EvelynModToggleExtra/addToBlacklist", "Skipping due to being currently required: " + metadatas.Key + " / " + file);
                        continue;
                    }
                    onAddToBlacklist(orig, self, metadatas.Key);
                    if (modToggles.ContainsKey(metadatas.Key)) modToggles[metadatas.Key].Index = 0;
                }
            }
        }

        public void onAddFileToMenu(On.Celeste.Mod.UI.OuiModToggler.orig_addFileToMenu orig, Mod.UI.OuiGenericMenu self, TextMenu menu, string file) {
            var self_data = new DynamicData(self);
            var modToggles = self_data.Get<Dictionary<string, TextMenu.OnOff>>("modToggles");
            var allMods = self_data.Get<List<string>>("allMods");
            var blacklistedMods = self_data.Get<HashSet<string>>("blacklistedMods");

            if ((Settings.HideDep && Dependencies.Contains(file)) || (Settings.HideWhitelist && Everest.Loader.Whitelist != null && Everest.Loader.Whitelist.Contains(file))) {
                // convince the rest of this class it's not being fucked with
                var disabled = Everest.Loader.Blacklist.Contains(file);
                TextMenu.OnOff option = new TextMenu.OnOff("Invisible", !disabled);
                if (disabled) blacklistedMods.Add(file);
                modToggles[file] = option;
                allMods.Add(file); 
                // like, seriously, why are these side effects part of addFileToMenu??
                // TODO - if i ever need this to support the favorites system, here it is
            } else {
                orig(self, menu, file);
            }
        }

        public void onDownloadDependency(On.Celeste.Mod.UI.OuiDependencyDownloader.orig_downloadDependency orig, Mod.UI.OuiLoggedProgress self, Mod.Helpers.ModUpdateInfo mod, EverestModuleMetadata installedVersion) {
            orig(self, mod, installedVersion);
            if (Settings.AutoDep && installedVersion == null) { // i think it's safe to ignore if it's already installed, but i'm honestly not entirely sure
                Dependencies.Add($"{mod.Name}.zip");
                SaveDependencyList();
            }
        }
    }

    class OuiToggleDepSubmenu : OuiGenericMenu, OuiModOptions.ISubmenu {
        public override string MenuName => Dialog.Clean("Evelyn_ModToggleExtra_Settings_ChangeDep");

        protected override void addOptionsToMenu(TextMenu menu) {
            // oh lord
            foreach (string filepath in Directory.GetFiles(Everest.Loader.PathMods)) {
                string filename = Path.GetFileName(filepath);
                if (!filename.EndsWith(".zip")) continue;

                TextMenu.OnOff button = new TextMenu.OnOff(filename, EvelynModToggleExtraModule.Dependencies.Contains(filename));
                button.Change(enabled => {
                    if (enabled) {
                        EvelynModToggleExtraModule.Dependencies.Add(filename);
                    } else {
                        EvelynModToggleExtraModule.Dependencies.Remove(filename);
                    }
                });
                menu.Add(button);
            }
        }

        public override void Update() {
            if (menu != null && menu.Focused && Selected && canGoBack && Input.MenuCancel.Pressed) {
                onBackPressed();
            }

            base.Update();
        }

        public void onBackPressed() {
            Logger.Log(LogLevel.Info, "OuiToggleDepSubmenu/onBackPressed", "Saving Dependency List");
            EvelynModToggleExtraModule.SaveDependencyList();
        }
    }
}
