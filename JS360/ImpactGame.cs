using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Impact
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class ImpactGame : Microsoft.Xna.Framework.Game
    {

#if !XBOX
        internal class GenerateInfo
        {
            public string source;
            public string destination;
            public string name;
        }
        internal GenerateInfo GenerateAndExit = null;
#endif

        public static ImpactGame instance;

        public GraphicsDeviceManager graphics;
        public Jurassic.ScriptEngine js;
        public JSCanvasInstance screenCanvas;
        public string path = "Game";
        public TimerManager timers = null;

        public ImpactGame()
        {
            ImpactGame.instance = this;
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;
        }

        protected override void Initialize()
        {
            timers = new TimerManager();

            js = new Jurassic.ScriptEngine();
            js.SetGlobalFunction("setTimeout", new Action<Jurassic.Library.FunctionInstance, int>(setTimeout));
            js.SetGlobalFunction("setInterval", new Action<Jurassic.Library.FunctionInstance, int>(setInterval));
            js.SetGlobalFunction("clearTimeout", new Action<int>(clearTimeout));
            js.SetGlobalFunction("clearInterval", new Action<int>(clearInterval));

            js.SetGlobalValue("window", js.Global);
            js.SetGlobalValue("console", new JSConsole(js));
            js.SetGlobalValue("Canvas", new JSCanvasConstructor(js));
            js.SetGlobalValue("Image", new JSImageConstructor(js));

#if !XBOX
            if (GenerateAndExit != null)
            {
                GenerateAssemblyAndExit(GenerateAndExit);
                return;
            }

#endif

#if XBOX || RELEASE
            // On the XBOX or RELEASE config, run the compiled JavaScript from the Assembly 
            js.LoadFromAssembly("ImpactGame");
            Generated.Main(js, js.CreateGlobalScope(), js.Global);
#else
            // In Windows/DEBUG, run JavaScript directly from source
            js.EnableDebugging = true;
            js.Evaluate(new Jurassic.FileScriptSource("Game/index.js"));
#endif
            base.Initialize();
        }

        // set/clear timeout/inveral
        public void setTimeout(Jurassic.Library.FunctionInstance callback, int ms)
        {
            timers.Create((float)ms / 1000, false, () => callback.Call(js.Global));
        }

        public void setInterval(Jurassic.Library.FunctionInstance callback, int ms)
        {
            float target = ms < 34 ? 0 : (float)ms / 1000; // make sure a timer with a target <= 33ms is fired each frame
            timers.Create(target, true, () => callback.Call(js.Global));
        }

        public void clearTimeout(int id) { timers.Remove(id); }
        public void clearInterval(int id) { timers.Remove(id); }



        protected override void LoadContent()
        {
            // TODO: use this.Content to load your game content here
        }

        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            base.Update(gameTime);
        }


        protected override void Draw(GameTime gameTime)
        {
            if (this.screenCanvas != null)
                screenCanvas.prepareFrame();

            timers.Update(gameTime);

            if (this.screenCanvas != null)
                screenCanvas.endFrame();

            base.Draw(gameTime);
        }


#if !XBOX
        public void GenerateAssembly(string sourceFile, string path, string name)
        {
            GenerateAndExit = new GenerateInfo();
            GenerateAndExit.name = name;
            GenerateAndExit.source = sourceFile;
            GenerateAndExit.destination = path;            
        }


        internal void GenerateAssemblyAndExit(GenerateInfo info)
        {
            System.IO.StreamReader streamReader = new System.IO.StreamReader(info.source);
            string source = streamReader.ReadToEnd();
            streamReader.Close();

            js.CompileToFile(source, info.destination, info.name);

            // Fix reference to mscorlib
            string fullName = string.Format("{0}/{1}.dll", info.destination, info.name);
            var assembly = new NETAssembly.NETAssembly();
            assembly.Load(fullName);
            assembly.SetVersionForReference(
                "mscorlib",
                new byte[] { 0x02, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00 }, // 2.0.5.0
                new byte[] { 0x7c, 0xec, 0x85, 0xd7, 0xbe, 0xa7, 0x79, 0x8e }
            );
            this.Exit();
        }
#endif //!XBOX

    }
}
