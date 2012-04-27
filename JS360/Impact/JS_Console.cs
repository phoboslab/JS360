using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Jurassic;
using Jurassic.Library;

namespace Impact
{
    public class JSConsole : ObjectInstance
    {
        public JSConsole(ScriptEngine engine)
            : base(engine)
        {
            this.PopulateFunctions();
        }

        [JSFunction(Name = "log")]
        public static void log(params object[] items)
        {
            var message = new System.Text.StringBuilder();
            for (int i = 0; i < items.Length; i++)
            {
                message.Append(' ');
                message.Append(TypeConverter.ToString(items[i]));
            }

            // Output the message to the console.
            System.Console.WriteLine(message.ToString());
        }
    }
}
