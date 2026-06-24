using System;

namespace SlimeNull.DuckovInterop.Infrastructure
{
    public abstract class FeatureBase
    {
        public bool IsEnabled { get; private set; }

        protected FeatureContext Context { get; private set; } = null!;
        public abstract string Name { get; }

        public void Enable(FeatureContext context)
        {
            if (IsEnabled)
            {
                if (ReferenceEquals(Context, context))
                {
                    return;
                }

                throw new InvalidOperationException("Already enabled with a different context.");
            }

            Context = context;
            OnEnable();
            IsEnabled = true;
        }

        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            OnDisable();
            IsEnabled = false;
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
