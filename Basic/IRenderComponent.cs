using System;
using UnityEngine;

namespace Core.ResourceRender
{

    /*! IRenderComponent
     \brief
      RENDER组件基类，对OBJECT实体行为的约束及封装，
      即实体的行为都应组件的形式存在。
     */
    public abstract class IRenderComponent : IDisposable
    {
        public IRenderObject RenderObject { get; private set; }
        private bool enabled_;

        public bool enabled
        {
            get
            {
                return enabled_;
            }
            set
            {
                if (enabled_ == value)
                    return;
                enabled_ = value;
                if (value)
                    OnEnabled();
                else
                    OnDisable();
            }
        }

        internal void Create(IRenderObject owner)
        {
            this.RenderObject = owner;
            this.enabled_ = true;
            OnCreate();
            OnEnabled();
        }

        internal void Destroy()
        {
            Dispose();
        }

        public virtual void Update() { }

        public virtual void LateUpdate() { }

        protected virtual void OnCreate() { }
        protected virtual void OnDestroy() { }

        protected virtual void OnEnabled() { }
        protected virtual void OnDisable() { }

        public void Dispose()
        {
            OnDestroy();
            this.RenderObject = null;
        }
    }
}
