using System;
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
    public class JSCanvasConstructor : ClrFunction
    {
        public JSCanvasConstructor(ScriptEngine engine)
            : base(engine.Function.InstancePrototype, "Canvas", new JSCanvasInstance(engine.Object.InstancePrototype))
        {
        }

        [JSConstructorFunction]
        public JSCanvasInstance Construct()
        {
            return new JSCanvasInstance(this.InstancePrototype);
        }
    }


    public class JSCanvasInstance : ObjectInstance
    {

        SpriteBatch batch = null;
        bool framePrepared = false;
        Texture2D solidRect = null;

        protected string _fillStyleHex = "ff00ff";
        Color _fillStyle = Color.Pink;

        public JSCanvasInstance(ObjectInstance prototype)
            : base(prototype)
        {
            this["width"] = 360;
            this["height"] = 240;
            this["globalAlpha"] = 1;
            this["fillStyle"] = "#000000";
            this["globalCompositeOperation"] = "source-over";

            ImpactGame.instance.screenCanvas = this;
            this.batch = new SpriteBatch(ImpactGame.instance.graphics.GraphicsDevice);

            solidRect = new Texture2D(ImpactGame.instance.graphics.GraphicsDevice, 1, 1);
            solidRect.SetData(new[] { Color.White });

            this.PopulateFunctions();
            this.PopulateFields();
        }

        [JSProperty(Name = "fillStyle")]
        public string fillStyle
        {
            get
            {
                return _fillStyleHex;
            }

            set
            {
                if (value is string)
                {
                    this._fillStyleHex = value;
                    if (this._fillStyleHex.StartsWith("#"))
                        this._fillStyleHex = this._fillStyleHex.Substring(1);
                    uint hex = uint.Parse(this._fillStyleHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    Color color = Color.White;
                    if (this._fillStyleHex.Length == 6)
                    {
                        color.R = (byte)(hex >> 16);
                        color.G = (byte)(hex >> 8);
                        color.B = (byte)(hex);
                    }
                    this._fillStyle = color;
                }
            }
        }

        public void prepareFrame()
        {
            this.batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            this.framePrepared = true;
        }

        public void endFrame()
        {
            if (this.framePrepared)
                this.batch.End();
            this.framePrepared = false;
        }

        [JSFunction(Name = "drawImage")]
        public void drawImage(JSImageInstance image, double dx, double dy)
        {
            if (!this.framePrepared) return;
            this.batch.Draw(image.texture, new Vector2((float)dx, (float)dy), Color.White);
        }

        [JSFunction(Name = "drawImage")]
        public void drawImage(JSImageInstance image, int dx, int dy, int dw, int dh)
        {
            if (!this.framePrepared) return;
            this.batch.Draw(image.texture, new Rectangle(dx, dy, dw, dh), null, Color.White);
        }

        [JSFunction(Name = "drawImage")]
        public void drawImage(JSImageInstance image, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
        {
            if (!this.framePrepared) return;
            this.batch.Draw(image.texture, new Rectangle(dx, dy, dw, dh), new Rectangle(sx, sy, sw, sh), Color.White);
        }

        [JSFunction(Name = "save")]
        public void save()
        {
        }

        [JSFunction(Name = "restore")]
        public void restore()
        {
        }

        [JSFunction(Name = "translate")]
        public void translate()
        {
        }

        [JSFunction(Name = "scale")]
        public void scale()
        {
        }

        [JSFunction(Name = "rotate")]
        public void rotate()
        {
        }

        [JSFunction(Name = "fillRect")]
        public void fillRect(int x, int y, int w, int h)
        {
            if (!this.framePrepared) return;
            batch.Draw(solidRect, new Rectangle(x, y, w, h), this._fillStyle);
        }

        [JSFunction(Name = "getContext")]
        public JSCanvasInstance getContext(string context)
        {
            return this;
        }        
    }
}
