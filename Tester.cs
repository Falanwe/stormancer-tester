using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Concurrent;

using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;

namespace Base
{
    public class Startup : IStartup
    {
        public void Run(IAppBuilder builder)
        {
            builder.SceneTemplate("tester", scene => TesterBehavior.AddTesterBehaviorToScene(scene));
        }
    }
}
