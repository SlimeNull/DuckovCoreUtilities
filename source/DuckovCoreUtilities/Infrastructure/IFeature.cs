namespace SlimeNull.DuckovCoreUtilities.Infrastructure
{
    public abstract class FeatureBase
    {
        protected FeatureContext Context { get; private set; } = null!;
        public abstract string Name { get; }

        public void Enable(FeatureContext context)
        {
            Context = context;
            OnEnable();
        }

        public void Disable()
        {
            OnDisable();
        }

        public virtual void Tick()
        {
        }

        public virtual void OnGUI()
        {
        }

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
        }
    }
}
