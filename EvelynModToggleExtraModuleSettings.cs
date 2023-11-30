using YamlDotNet.Serialization;
using Celeste.Mod.UI;

namespace Celeste.Mod.EvelynModToggleExtra {

    public class EvelynModToggleExtraModuleSettings : EverestModuleSettings {

        // alright what settings do i need
        // dependency list path
        // toggle hiding deps
        // TODO - hide mods from whitelist
        // TODO - mark auto install deps as deps
        // submenu for switching dependencies
        
        [SettingName("Evelyn_ModToggleExtra_Settings_DepPath")]
        [SettingMaxLength(30)]
        [SettingNeedsRelaunch]
        public string DepPath {get; set;}

        [SettingName("Evelyn_ModToggleExtra_Settings_HideDep")]
        public bool HideDep {get; set;}

        [SettingName("Evelyn_ModToggleExtra_Settings_HideWhitelist")]
        public bool HideWhitelist {get; set;}

        [SettingName("Evelyn_ModToggleExtra_Settings_AutoDep")]
        public bool AutoDep {get; set;}

        [SettingName("Evelyn_ModToggleExtra_Settings_Changedep")]
        [YamlIgnore]
        public int ChangeDep {get; set;} = 0;

        public void CreateChangeDepEntry(TextMenu menu, bool inGame) {
            if (!inGame)
                menu.Add(new TextMenu.Button(Dialog.Get("Evelyn_ModToggleExtra_Settings_ChangeDep"))
                        .Pressed(() => OuiGenericMenu.Goto<OuiToggleDepSubmenu>(overworld => overworld.Goto<OuiModOptions>(), new object[0])));
        }

    }
}
