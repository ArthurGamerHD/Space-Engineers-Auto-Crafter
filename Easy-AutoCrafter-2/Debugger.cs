using System;

namespace IngameScript
{
    public partial class Program
    {
        public static class DebuggerHelper
        {
            public static void Break()
            {
                try
                {
                    throw new DebugRequest();
                }
                catch
                {
                    /* workaround for Debugger.Attach() not available for Mods */
                }
            }

            public class DebugRequest : Exception
            {
                public override string Message => "Hello DNSpy";
            }
        }
    }
}