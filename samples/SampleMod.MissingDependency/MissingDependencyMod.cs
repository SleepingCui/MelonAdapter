using MelonLoader;

[assembly: MelonInfo(typeof(SampleMod.MissingDependency.MissingDependencyMod), "Missing Dependency Mod", "1.0.0", "MelonAdapter")]
[assembly: MelonAdditionalDependencies("ModThatIsNotInstalled")]

namespace SampleMod.MissingDependency
{
    /// <summary>
    /// This mod must never load: its required dependency does not exist. MelonAdapter reports it as
    /// rejected instead of letting the mod fail later at runtime.
    /// </summary>
    public class MissingDependencyMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("This line must never appear.");
        }
    }
}
