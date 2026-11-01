using System;
using MelonLoader;

[assembly: MelonInfo(typeof(SampleMod.Throwing.ThrowingMod), "Throwing Mod", "1.0.0", "MelonAdapter")]

namespace SampleMod.Throwing
{
    /// <summary>
    /// Throws from every update callback. MelonAdapter must isolate the exception, log it, and disable
    /// this mod once <c>lifecycle.maxConsecutiveExceptions</c> is reached without affecting other mods.
    /// </summary>
    public class ThrowingMod : MelonMod
    {
        public override void OnUpdate()
        {
            throw new InvalidOperationException("sample failure in OnUpdate");
        }
    }
}
