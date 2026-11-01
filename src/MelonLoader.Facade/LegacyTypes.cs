using System;

namespace MelonLoader
{
    /// <summary>
    /// [MelonInfo] variant used by MelonPlugin types (the mod variant lives in Attributes.cs).
    /// </summary>
    public class MelonPluginInfoAttribute : MelonInfoAttribute
    {
        public MelonPluginInfoAttribute(Type type, string name, string version, string author, string downloadLink = null)
            : base(type, name, version, author, downloadLink)
        {
        }
    }

    /// <summary>
    /// [MelonGame] variant used by MelonPlugin types; the game name is exposed as GameName.
    /// </summary>
    public class MelonPluginGameAttribute : MelonGameAttribute
    {
        public MelonPluginGameAttribute(string developer = null, string gameName = null)
            : base(developer, gameName)
        {
        }

        public new string Developer => base.Developer;

        public string GameName => Name;
    }

    /// <summary>
    /// A plugin is a MelonLoader component that runs before mods and may replace the melon
    /// resolution step. MelonAdapter loads plugins from the same Mods directory and validates their
    /// metadata exactly like mods; the plugin specific callbacks are dispatched from the lifecycle.
    /// </summary>
    public abstract class MelonPlugin : MelonTypeBase<MelonPlugin>
    {
        public virtual void OnPreInitialization()
        {
        }

        public override void OnApplicationEarlyStart()
        {
        }

        public virtual void OnPreModsLoaded()
        {
        }

        public virtual void OnApplicationStarted()
        {
        }

        public MelonPluginInfoAttribute InfoAttribute => Info as MelonPluginInfoAttribute;

        public MelonPluginGameAttribute[] GameAttributes
        {
            get
            {
                MelonGameAttribute[] games = Games;
                if (games == null || games.Length == 0)
                {
                    return new MelonPluginGameAttribute[0];
                }

                MelonPluginGameAttribute[] typed = new MelonPluginGameAttribute[games.Length];
                int count = 0;
                foreach (MelonGameAttribute game in games)
                {
                    MelonPluginGameAttribute pluginGame = game as MelonPluginGameAttribute;
                    if (pluginGame != null)
                    {
                        typed[count++] = pluginGame;
                    }
                }

                if (count == typed.Length)
                {
                    return typed;
                }

                MelonPluginGameAttribute[] trimmed = new MelonPluginGameAttribute[count];
                Array.Copy(typed, trimmed, count);
                return trimmed;
            }
        }
    }
}
