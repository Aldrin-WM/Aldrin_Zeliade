using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Mrc;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class Codings
    {
        private const string XllName = "Codings";

        [WorksheetFunction(XllName + ".BusinessCenters")]
        public static string[] BusinessCenterCodings()
        {
            return BusinessCenters.Codings.Values.ToArray();
        }

        [WorksheetFunction(XllName + ".BusinessDayConventions")]
        public static string[] BusinessDayConventionCodings()
        {
            return BusinessDayConventions.Codings.Values.ToArray();
        }

        [WorksheetFunction(XllName + ".DayCountConventions")]
        public static string[] DayCountConventionCodings()
        {
            return DayCountConventions.Codings.Values.ToArray();
        }

        [WorksheetFunction(XllName + ".RollConventions")]
        public static string[] RollConventionCodings()
        {
            return RollConventions.Codings.Values.ToArray();
        }

        [WorksheetFunction(XllName + ".CompoundingRateType")]
        public static string[] CompoundingRateTypeCodings()
        {
            return Enum.GetNames(typeof(CompoundingRateType));
        }

        [WorksheetFunction(XllName + ".StubPeriodType")]
        public static string[] StubPeriodTypeCodings()
        {
            return Enum.GetNames(typeof(StubPeriodType));
        }

        [WorksheetFunction(XllName + ".QuoteBumpType")]
        public static string[] QuoteBumpTypeCodings()
        {
            return Enum.GetNames(typeof(QuoteBumpType));
        }

        [WorksheetFunction(XllName + ".ResetType")]
        public static string[] ResetType()
        {
            return Enum.GetNames(typeof(ResetType));
        }

        [WorksheetFunction(XllName + ".BumpSheetSetType")]
        public static string[] BumpSheetSetType()
        {
            var asm = AppDomain.CurrentDomain.Load("AldrinAnalytics");
            var types = asm.GetTypes();
            var output = new List<string>();
            foreach (var t in asm.GetTypes())
            {                
                if (t.GetInterfaces().Contains(typeof(IBumpSheetTypeSet)))
                {
                    var tmp = t.Name.Split('.').Last();
                    tmp = tmp.Replace("'1", ""); // remove generic
                    output.Add(tmp);
                }
            }
            return output.ToArray() ;
            
        }

        [WorksheetFunction(XllName + ".InstrumentType")]
        public static string[] InstrumentType()
        {
            var asm = AppDomain.CurrentDomain.Load("AldrinAnalytics");
            var types = asm.GetTypes();
            var output = new List<string>();
            foreach (var t in asm.GetTypes())
            {
                if (t.GetInterfaces().Contains(typeof(IInstrument)))
                {
                    var tmp = t.Name.Split('.').Last();
                    tmp = tmp.Replace("'1", ""); // remove generic
                    output.Add(tmp);
                }
            }

            asm = AppDomain.CurrentDomain.Load("Zeliade.Finance.Common");
            types = asm.GetTypes();
            foreach (var t in asm.GetTypes())
            {
                if ( t.IsSubclassOf(typeof(RateInstrument)))
                {
                    var tmp = t.Name.Split('.').Last();
                    output.Add(tmp);
                }
            }

            return output.ToArray();

        }
    }
}
