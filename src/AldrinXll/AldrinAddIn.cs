using ExcelDna.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ZAddIn;
using ZAddIn.XlConfig;
using Zeliade.Finance.Mrc;
using Log = ExcelDna.Logging.LogDisplay;

namespace AldrinXll
{
    public class AldrinAddIn : AddIn
    {
        public override void AutoOpen()
        {
            ExcelIntegration.RegisterUnhandledExceptionHandler(ex => "ERROR: " + ex.ToString());

            LoadAssemblies();
            MarketConventionsFactory.DefaultConfiguration();
            MarketConventionsFactory.Configure(new Zeliade.Finance.Mrc.Configuration.DefaultMrcAliasBuilder());

            RegisterCtors();
            RegisterMethods();
            RegisterProps();
        }

        private static void LoadAssemblies()
        {
            _assemblies = new List<Assembly>();

            string[] assemblies = new string[]
            {
                "Zeliade.Common",
                "Zeliade.Math" ,
                "Zeliade.Finance.Mrc",
                "Zeliade.Finance.Common",
                "AldrinAnalytics",
                "ZQFXLLObjects"
            };

            foreach (string asmName in assemblies)
            {
                try
                {
                    _assemblies.Add(AppDomain.CurrentDomain.Load(asmName));
                    Log.WriteLine(asmName + " loaded.");
                }
                catch (Exception e)
                {
                    Log.WriteLine(asmName + " not loaded.");
                }
            }
        }
    }
}
