using System;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Mrc;
using AldrinAnalytics.Models;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Instruments
{    

    public abstract class RateReference : Ticker
    {
        public RateReference(string name, string refCurrency) : base(name, refCurrency)
        {
        }

        public abstract double Fixing(IJointModel model, Type quoteType);

    }

    public class LiborReference : RateReference
    {
        private const string XllName = "Libor";
        public const string BusinessType = "libor";

        public IPeriod Tenor{get; private set; }
        public IDayCountFraction DayCount { get; private set; }

        [WorksheetFunction(XllName+".New")]
        public LiborReference(string ccy, IPeriod tenor, IDayCountFraction dcf)
            : base(string.Format("LIBOR_{0}_{1}", ccy, tenor.Coding), ccy)
        {
            Tenor = tenor ?? throw new ArgumentNullException(nameof(tenor));
            DayCount = Require.ArgumentNotNull(dcf,nameof(dcf));
        }




        //public override bool Equals(object obj)
        //{
        //    return base.Equals(obj);
        //}

        //public override int GetHashCode()
        //{
        //    var baseHash = base.GetHashCode();
        //    var tenorHash = Tenor.GetHashCode();
        //    int hash = 17;
        //    const int p2 = 23;
        //    hash = hash * p2 + baseHash;
        //    hash = hash * p2 + tenorHash;
        //    return hash;
        //}

        public override string ToString()
        {
            return string.Format("LIBOR_{0}_{1}", ReferenceCurrency, Tenor.Coding);
        }

        public override double Fixing(IJointModel model, Type quoteType)
        {
            return model.LiborFixing(this, quoteType);
        }
    }

    public class OisReference : RateReference
    {
        private const string XllName = "OisReference";
        public const string BusinessType = "ois";

        public IPeriod Tenor { get; set; }
        public IDayCountFraction DayCount { get; private set; }

        [WorksheetFunction(XllName+".New")]
        public OisReference(string refCurrency, IDayCountFraction dcf) 
            : base(string.Format("OIS_{0}", refCurrency), refCurrency)
        {
            DayCount = Require.ArgumentNotNull(dcf, nameof(dcf));
        }

        public override double Fixing(IJointModel model, Type quoteType)
        {
            return model.OisFixing(this, quoteType);
        }
    }
}
