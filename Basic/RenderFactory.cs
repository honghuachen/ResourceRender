using System;
using System.Collections.Generic;

namespace Core.ResourceRender
{
    public class RenderFactory : CLoopObject
    {
        List<IRenderObject> renderObjs = new List<IRenderObject>();
        static RenderFactory instance = null;

        public override void Initialize()
        {
            instance = this;
        }

        private object AllocInstance(Type type, params object[] args)
        {
            if (args != null && args.Length > 0)
                return Activator.CreateInstance(type, args);
            else
                return Activator.CreateInstance(type);
        }

        public static T CreateInstance<T>(string filename, IRenderObject parent, params object[] args) where T : IRenderObject
        {
            filename = filename.ToLower();
            Type type = typeof(T);
            return CreateInstance(type, filename, parent, args) as T;
        }

        public static IRenderObject CreateInstance(Type type, string filename, IRenderObject parent, params object[] args)
        {
            if(instance == null)
            {
                LOG.LogError("'RenderFactory' is null.Please initialize it.");
                return null;
            }
            filename = filename.ToLower();
            IRenderObject inst = instance.AllocInstance(type, args) as IRenderObject;
            inst.LoadAsset(filename, parent);
            instance.renderObjs.Add(inst);
            return inst;
        }

        internal static void RemoveRenderObject(IRenderObject obj)
        {
            instance.renderObjs.Remove(obj);
        }

        public override void Update()
        {
            for(int i = 0;i < renderObjs.Count; i++)
            {
                renderObjs[i].Update();
            }
        }

        public override void LateUpdate()
        {
            for (int i = 0; i < renderObjs.Count; i++)
            {
                renderObjs[i].LateUpdate();
            }
        }
    }
}
