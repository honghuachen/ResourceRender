using System;
using System.Collections.Generic;
using UnityEngine;
using ZCore.InternalUtil;

namespace Core.ResourceRender
{
    public abstract class IRenderObject : PListNode<IRenderObject>, IDisposable
    {
        public bool complete { get; protected set; } = false;
        public UnityEngine.Object asset { get; private set; } = null;
        public string name;
        private bool active = true;
        private bool visible = true;
        private Vector3 initLocalPosition = Vector3.zero;
        private Vector3 initLocalScale = Vector3.one;
        private Quaternion initRotation = Quaternion.identity;
        private Transform parentTransform = null;
        private bool isSetParentTransform = false;
        private int layer;
        private IRenderObject parent;
        private PList<IRenderObject> children;
        private ComponentMapping components;
        private List<IRenderComponent> tempComponents = new List<IRenderComponent>();//用于临时缓存，在update中用于for循环遍历，减少gc
        private TimerEvent timer;
        private Dictionary<IRenderComponent, bool> dict = null;

        #region component mapping
        public T AddComponent<T>() where T : IRenderComponent, new()
        {
            if (components == null)
                components = new ComponentMapping();
            Type type = typeof(T);
            if (components[type, type] != null)
            {
                throw new Exception(string.Format("can't add component more than one with the same type, type:{0}", typeof(T).Name));
            }
            T c = new T();
            c.Create(this);
            components[type, type] = c;
            tempComponents.Add(c);
            return c;
        }

        public T AddComponent<U, T>()
            where T : U, new()
            where U : IRenderComponent
        {
            if (components == null)
                components = new ComponentMapping();

            Type tbas = typeof(U);
            Type tsub = typeof(T);
            if (components[tbas, tsub] != null)
            {
                throw new Exception(string.Format("can't add component more than one with the same type, type:{0}", typeof(T).Name));
            }
            T c = new T();
            ((U)c).Create(this);
            components[tbas, tsub] = c;
            tempComponents.Add(c);
            return c;
        }

        public void RemoveComponent<T>() where T : IRenderComponent
        {
            if (components == null)
                return;
            Type type = typeof(T);
            IRenderComponent c = components[type, type];
            if (c != null)
            {
                c.Destroy();
                components[type, type] = null;
                tempComponents.Remove(c);
            }
        }

        public T GetComponent<T>() where T : IRenderComponent
        {
            if (components == null)
                return default(T);

            Type type = typeof(T);
            IRenderComponent c = components[type, type];
            return (c as T);
        }

        class ComponentMapping : Dictionary<Type, KeyValuePair<Type, IRenderComponent>>
        {
            public IRenderComponent this[Type tbas, Type tsub]
            {
                get
                {
                    KeyValuePair<Type, IRenderComponent> kv = default(KeyValuePair<Type, IRenderComponent>);
                    if (this.TryGetValue(tbas, out kv))
                    {
                        return kv.Value;
                    }
                    else
                    {
                        if (this.TryGetValue(tsub, out kv))
                        {
                            return kv.Value;
                        }
                    }
                    return default(IRenderComponent);
                }
                set
                {
                    if (value == null)
                    {
                        KeyValuePair<Type, IRenderComponent> kv = default(KeyValuePair<Type, IRenderComponent>);
                        if (this.TryGetValue(tbas, out kv))
                        {
                            this.Remove(tbas);
                            this.Remove(kv.Key);
                        }
                        else
                        {
                            if (this.TryGetValue(tsub, out kv))
                            {
                                this.Remove(tsub);
                                this.Remove(kv.Key);
                            }
                        }
                        return;
                    }
                    this[tbas] = new KeyValuePair<Type, IRenderComponent>(tsub, value);
                    this[tsub] = new KeyValuePair<Type, IRenderComponent>(tbas, value);
                }
            }
        }
        #endregion
        public GameObject gameObject { get; protected set; }

        public IRenderObject Parent
        {
            get { return this.parent; }
            set
            {
                if (this.parent != null)
                    this.parent.RemoveChild(this);

                this.parent = value;
                if (this.parent != null)
                    this.parent.AddChild(this);
            }
        }

        public Transform ParentTransform
        {
            get { return this.parentTransform; }
            set
            { // ensure that the parent transform is
                if (value == null)
                {
                    // fix when parent tranform has been destroyed after create
                    if (this.isSetParentTransform)
                        this.Destroy();
                    return;
                }

                if (this.gameObject != null)
                    this.gameObject.transform.parent = value;
                this.parentTransform = value;
                this.isSetParentTransform = true;
            }
        }

        public Vector3 LocalPosition
        {
            get
            {
                if (this.gameObject != null && this.gameObject.transform != null)
                    return this.gameObject.transform.localPosition;
                else
                    return initLocalPosition;
            }
            set
            {
                if (this.gameObject != null && this.gameObject.transform != null)
                    this.gameObject.transform.localPosition = value;
                this.initLocalPosition = value;
            }
        }

        public void SetRotation(Quaternion rotation)
        {
            if (this.gameObject != null && this.gameObject.transform != null)
            {
                this.gameObject.transform.localRotation = rotation;
            }
            this.initRotation = rotation;
        }

        public void SetRotation(Vector3 euler)
        {
            Quaternion rotation = Quaternion.Euler(euler);
            SetRotation(rotation);
        }

        public Vector3 Forward
        {
            get
            {
                if (this.gameObject != null && this.gameObject.transform != null)
                    return this.gameObject.transform.forward;
                else
                {
                    if (initRotation != Quaternion.identity)
                        return initRotation.eulerAngles;
                    else
                        return Vector3.zero;
                }
            }
            set
            {
                Quaternion quaternion = Quaternion.LookRotation(value);
                SetRotation(quaternion);
            }
        }

        public Vector3 LocalScale
        {
            get { return this.initLocalScale; }
            set
            {
                if (this.gameObject != null && this.gameObject.transform != null)
                    this.gameObject.transform.localScale = value;
                this.initLocalScale = value;
            }
        }

        public bool Visible
        {
            get { return visible; }
            set
            {
                this.visible = value;

                if (this.gameObject != null)
                {
                    Renderer[] components = this.gameObject.GetComponentsInChildren<Renderer>(true);
                    if (components != null)
                    {
                        for (int i = 0; i < components.Length; ++i)
                        {
                            Renderer renderer = components[i];
                            renderer.enabled = this.visible;
                        }
                    }

                    if (this.gameObject.GetComponent<Renderer>() != null)
                        this.gameObject.GetComponent<Renderer>().enabled = this.visible;

                    Terrain terrain = this.gameObject.GetComponent<Terrain>();
                    if (terrain != null)
                        terrain.enabled = this.visible;

                    Light light = this.gameObject.GetComponent<Light>();
                    if (light != null)
                        light.enabled = this.visible;
                }

                OnVisible();
            }
        }

        public bool Active
        {
            get
            {
                if (this.gameObject)
                    return this.gameObject.activeSelf;

                return this.active;
            }
            set
            {
                this.active = value;
                if (this.gameObject && this.gameObject.activeSelf != value)
                    this.gameObject.SetActive(value);
            }
        }

        public int Layer
        {
            get { return this.layer; }
            set
            {
                if (value == 0)
                    return;
                this.layer = value;

                if (children != null)
                {
                    for (var n = children.next; n != children; n = n.next)
                    {
                        IRenderObject child = (IRenderObject)n;
                        if (child.layer == 0)
                            child.layer = value;
                    }
                }

                if (this.gameObject != null)
                    SetLayerRecursively(this.gameObject);
            }
        }

        private void AddChild(IRenderObject child)
        {
            if (children == null)
            {
                children = new PList<IRenderObject>();
                children.Init();
            }
            children.AddTail(child);
            if (this.gameObject != null && child.gameObject != null)
                child.gameObject.transform.parent = this.gameObject.transform;
        }

        private void RemoveChild(IRenderObject child)
        {
            if (this.children == null)
                return;
            children.Remove(child);
            if (this.gameObject != null && child.gameObject != null && child.gameObject.transform.parent == this.gameObject.transform)
                child.gameObject.transform.parent = null;
        }

        private void SetLayerRecursively(GameObject go)
        {
            go.layer = this.layer;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetLayerRecursively(go.transform.GetChild(i).gameObject);
            }
        }

        private void ApplyParticleScaleRecursively(Transform go, float scale)
        {
            scale = Mathf.Abs(scale);
            if (!go || Mathf.Approximately(scale, 1))
                return;
            for (int i = 0; i < go.childCount; i++)
                ApplyParticleScaleRecursively(go.GetChild(i), scale);

            ParticleEmitter pe = go.GetComponent<ParticleEmitter>();
            if (pe)
            {
                pe.maxSize *= scale;
                pe.minSize *= scale;
                pe.rndVelocity *= scale;
                pe.worldVelocity *= scale;
                pe.localVelocity *= scale;
            }

            Renderer r = go.GetComponent<Renderer>();
            if (r && r is TrailRenderer)
            {
                TrailRenderer tr = r as TrailRenderer;
                if (tr != null)
                {
                    tr.startWidth *= scale;
                    tr.endWidth *= scale;
                }
            }

            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps)
            {
                ps.startSize *= scale;
                ps.startSpeed *= scale;
            }
        }

        private void Create(UnityEngine.Object asset)
        {
            this.asset = asset;
            this.OnCreate(asset);

            if (this.parent != null)
            {
                if (this.gameObject != null && this.parent.gameObject != null)
                    this.gameObject.transform.parent = this.parent.gameObject.transform;

                // Inherit the parent's layer, when child doesn't assign a layer.
                if (this.layer == 0 && this.parent.layer != 0)
                    this.layer = this.parent.layer;
            }

            ApplyInitPosition();

            if (children != null)
            {
                for (var n = children.next; n != children; n = n.next)
                {
                    IRenderObject child = (IRenderObject)n;
                    if (this.gameObject != null && child.gameObject != null)
                    {
                        child.gameObject.transform.parent = this.gameObject.transform;
                        child.ApplyInitPosition();
                    }
                }
            }
            Layer = this.layer;

            if (!this.visible)
                Visible = this.visible;

            if (!this.active)
                Active = this.active;
            complete = true;
        }

        private void ApplyInitPosition()
        {
            if (this.gameObject == null)
                return;
            ParentTransform = this.parentTransform;
            LocalPosition = this.initLocalPosition;
            SetRotation(this.initRotation);
            LocalScale = this.initLocalScale;
        }

        //--------------------------------------------------------------------
        internal void LoadAsset(string fileName, IRenderObject parent)
        {
            name = fileName;
            Parent = parent;
            AssetBundleManager.LoadAsset(fileName, (ret) =>
            {
                Create(ret);
            });
        }

        internal void Update()
        {
            if (!active)
                return;

            if (components != null)
            {
                if (dict == null)
                    dict = new Dictionary<IRenderComponent, bool>();
                dict.Clear();

                for (int i = 0; i < tempComponents.Count; i++)
                {
                    var c = tempComponents[i];
                    if (c == null)
                        continue;

                    if (dict.ContainsKey(c))
                        continue;
                    dict[c] = true;

                    if (c.enabled)
                        c.Update();
                }
            }
            OnUpdate();

            if (children != null)
            {
                for (var n = children.next; n != children;)
                {
                    if (n == null)
                    {
                        // children maybe all destroyed in child.Update()
                        // for fix NullReference exception.
                        break;
                    }
                    var next = n.next;

                    IRenderObject child = (IRenderObject)n;
                    if (child != null)
                        child.Update();

                    n = next;
                }
            }

            if (timer != null)
                timer.Process();
        }

        internal void LateUpdate()
        {
            if (!active)
                return;

            if (components != null)
            {
                if (dict == null)
                    dict = new Dictionary<IRenderComponent, bool>();
                dict.Clear();

                for (int i = 0; i < tempComponents.Count; i++)
                {
                    var c = tempComponents[i];
                    if (c == null)
                        continue;

                    if (dict.ContainsKey(c))
                        continue;
                    dict[c] = true;

                    if (c.enabled)
                        c.LateUpdate();
                }
            }
            OnLateUpdate();
        }

        public void AddTimer(TimerEventObject.TimerProc proc, object obj, int p1, int p2)
        {
            AddTimer(1, 1, proc, obj, p1, p2);
        }

        public void AddTimer(float time, TimerEventObject.TimerProc proc, object obj, int p1, int p2)
        {
            int frame = Convert.ToInt32(Application.targetFrameRate * time);
            AddTimer(frame, frame, proc, obj, p1, p2);
        }

        private void AddTimer(int start, int interval, TimerEventObject.TimerProc proc, object obj, int p1, int p2)
        {
            if (timer == null)
                timer = new TimerEvent(1);
            timer.Add(start, interval, proc, obj, p1, p2);
        }

        public void Destroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            try
            {
                RenderFactory.RemoveRenderObject(this);
                AssetBundleManager.UnloadAssetBundle(name);
                if (children != null)
                {
                    for (var n = children.next; n != children;)
                    {
                        var next = n.next;
                        IRenderObject child = (IRenderObject)n;
                        if (child != null)
                            child.Parent = null;
                        n = next;
                    }
                    children = null;
                }

                if (components != null)
                {
                    if (dict == null)
                        dict = new Dictionary<IRenderComponent, bool>();
                    dict.Clear();

                    for (int i = 0; i < tempComponents.Count; i++)
                    {
                        var c = tempComponents[i];
                        if (c == null)
                            continue;
                        if (dict.ContainsKey(c))
                            continue;
                        dict[c] = true;
                        c.Destroy();
                    }
                    tempComponents.Clear();

                    components.Clear();
                    components = null;
                }

                if (timer != null)
                    timer.Clear();

                this.Parent = null;
                this.OnDestroy();
            }
            catch (Exception e)
            {
                LOG.LogError(e.ToString(), this.gameObject);
            }
        }

        protected virtual void OnVisible() { }
        protected virtual void OnCreate(UnityEngine.Object asset) { }
        protected virtual void OnDestroy() { }
        protected virtual void OnUpdate() { }
        protected virtual void OnLateUpdate() { }
    }
}
