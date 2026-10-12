// Compatibility entrypoint. The obsolete cube obstacle builder is retired:
// never silently overwrite a production arena with its old placeholders.
public static class ChaseSceneBuilder
{
    public static void Build() => ChaseArenaV1Builder.Rebuild();
}
