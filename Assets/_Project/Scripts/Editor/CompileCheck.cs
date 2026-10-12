using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Does nothing: running it in batch mode just compiles the project (a quick check for errors).</summary>
    public static class CompileCheck
    {
        public static void Run() => Debug.Log("[CompileCheck] OK");
    }
}
