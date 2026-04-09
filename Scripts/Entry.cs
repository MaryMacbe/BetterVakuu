using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace BetterVakuu.Scripts;

// 蹇呴』瑕佸姞鐨勫睘鎬э紝鐢ㄤ簬娉ㄥ唽Mod銆傚瓧绗︿覆鍜屽垵濮嬪寲鍑芥暟鍛藉悕涓€鑷淬€?
[ModInitializer("Init")]
public class Entry
{
    // 鍒濆鍖栧嚱鏁?
    public static void Init()
    {
        // 鎵損atch锛堝嵆淇敼娓告垙浠ｇ爜鐨勫姛鑳斤級鐢?
        // 浼犲叆鍙傛暟闅忔剰锛屽彧瑕佷笉鍜屽叾浠栦汉鎾炶溅鍗冲彲
        var harmony = new Harmony("sts2.bettervakuu");
        harmony.PatchAll();
        // 浣垮緱tscn鍙互鍔犺浇鑷畾涔夎剼鏈?
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
        Log.Debug("Mod initialized!");
    }
}

