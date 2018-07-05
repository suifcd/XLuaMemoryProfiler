using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XLua;

/// <summary>
/// 使用时先通过菜单XLua/TimeProfile开启编辑器窗口
/// </summary>
public class MemoryProfileTest : MonoBehaviour
{
    private static LuaEnv luaenv = new LuaEnv();

    public static LuaEnv GetLuaEnv()
    {
        return luaenv;
    }

    private string ss =
        @"memory = require ('perf.memory')
           local a = 1 
           local b = {}
           local go = CS.UnityEngine.GameObject();
           ";

    private void Start()
    {
        luaenv = new LuaEnv();
        luaenv.DoString(ss);
    }

    private void OnDestroy()
    {
        luaenv.Dispose();
    }
}
