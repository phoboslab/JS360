using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Jurassic;
using Jurassic.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Impact
{
    public class JSImageConstructor : ClrFunction
    {
        public JSImageConstructor(ScriptEngine engine)
            : base(engine.Function.InstancePrototype, "Image", new JSImageInstance(engine.Object.InstancePrototype))
        {
        }

        [JSConstructorFunction]
        public JSImageInstance Construct()
        {
            return new JSImageInstance(this.InstancePrototype);
        }
    }



    public class JSImageInstance : ObjectInstance
    {
        protected string path;
        public Texture2D texture = null;


        public JSImageInstance(ObjectInstance prototype)
            : base(prototype)
        {
            this["width"] = 0;
            this["height"] = 0;

            this.PopulateFunctions();
            this.PopulateFields();
        }

        [JSProperty(Name = "src")]
        public string src
        {
            
            get
            {
                return path; 
            }
            
            set 
            {
                if (value != null)
                {
                    this.path = value.ToString();
                    this.Load();
                }
            }
        }


        public void Load()
        {
            try
            {
                Stream stream = File.OpenRead(String.Format("{0}/{1}", ImpactGame.instance.path, this.path));
                this.texture = Texture2D.FromStream(ImpactGame.instance.graphics.GraphicsDevice, stream);
                this["width"] = this.texture.Width;
                this["height"] = this.texture.Height;

                if (this["onload"] is Jurassic.Library.FunctionInstance)
                {
                   ((FunctionInstance)this["onload"]).Call(this);
                }
            }
            catch
            {
                if (this["onerror"] is Jurassic.Library.FunctionInstance)
                {
                   ((FunctionInstance)this["onerror"]).Call(this);
                }
            }
        }
    }
}
