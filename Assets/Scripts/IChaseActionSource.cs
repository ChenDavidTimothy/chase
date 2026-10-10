namespace Chase
{
    // Every controller, including a future learned policy, provides the same state.
    public interface IChaseActionSource
    {
        ChaseAction ReadAction(float clock);
        void ResetSource();
    }
}
