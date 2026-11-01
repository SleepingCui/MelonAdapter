namespace SupportLib
{
    /// <summary>
    /// Plain helper type without any MelonLoader dependency. MelonAdapter must treat the assembly that
    /// contains it as a private dependency of the mod that ships it, never as a mod on its own.
    /// </summary>
    public static class SupportLibBootstrap
    {
        public static string Ping()
        {
            return "support-lib-ok";
        }
    }
}
