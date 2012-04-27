using System;
using System.Collections.Generic;

namespace Impact
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (ImpactGame game = new ImpactGame())
            {
#if !XBOX
                if (args.Length == 3 && args[0].Equals("/generate"))
                {
                    string sourceFile = args[1];
                    string path = System.IO.Path.GetDirectoryName(args[2]);
                    string name = System.IO.Path.GetFileNameWithoutExtension(args[2]);

                    game.GenerateAssembly(sourceFile, path, name);
                }
#endif
                game.Run();
            }
            return;
        }
    }
#endif
}

