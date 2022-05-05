using System;
using System.Linq;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using System.Collections.Generic;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    public class FxCurveBootstrapper<I> : BidAskBootstrapper<IForwardForexCurve> where I : RateInstrument //IGenericBootstrapper<IForwardForexCurve> where I:RateInstrument
    {

        private const string XllName = "FxCurveBootstrapper";

        private readonly IGenericBootstrapper<IDiscountCurve<DateTime>> _oisBoot;
        
        public FxCurveBootstrapper(IGenericBootstrapper<IDiscountCurve<DateTime>> oisBoot)
        {
            _oisBoot = oisBoot ?? throw new ArgumentNullException(nameof(oisBoot));
        }

        //public Dictionary<Type, IForwardForexCurve> Bootstrap(DataQuoteSheet sheet)
        //{

        //    //var ccPairs = sheet.Data.Where(x => x is CurrencyPairSecurity).ToList();
        //    //Require.ArgumentListCount(1, ccPairs, "sheet.Data", Error.Msg("The input sheet should contain one and only one currency pair; {0} found !", ccPairs.Count));

        //    //var ccyPair = ccPairs.First() as CurrencyPairSecurity;

        //    var output = new Dictionary<Type, IForwardForexCurve>();

        //    var curve = Bootstrap<BidQuote>(sheet);
        //    if (curve!=null)
        //        output.Add()

        //    if (ccyPair.CcyPair.ForeignCurrency==ccyPair.CcyPair.DomesticCurrency)
        //    {
        //        if (ccyPair.ContainsQuote<BidQuote>())
        //            output.Add(typeof(BidQuote), new FxCurveOne(sheet.SpotDate, ccyPair.CcyPair));
        //        if (ccyPair.ContainsQuote<MidQuote>())
        //            output.Add(typeof(MidQuote), new FxCurveOne(sheet.SpotDate, ccyPair.CcyPair));
        //        if (ccyPair.ContainsQuote<AskQuote>())
        //            output.Add(typeof(AskQuote), new FxCurveOne(sheet.SpotDate, ccyPair.CcyPair));
        //        return output;
        //    }

        //    // Ois curves 
        //    var oisSheet = sheet.Data.Where(x => x is I).ToList();
        //    var domSheet = oisSheet.Where(x => (x as I).ReferenceCurrency == ccyPair.CcyPair.DomesticCurrency).ToList();
        //    var domCurves = _oisBoot.Bootstrap(new DataQuoteSheet(sheet.SpotDate, domSheet));
        //    var forSheet = oisSheet.Where(x => (x as I).ReferenceCurrency == ccyPair.CcyPair.ForeignCurrency).ToList();
        //    var foreignCurves = _oisBoot.Bootstrap(new DataQuoteSheet(sheet.SpotDate, forSheet));

        //    if (ccyPair.ContainsQuote<MidQuote>())
        //        output.Add(typeof(MidQuote), new FxForwardCurve(domCurves[typeof(MidQuote)], foreignCurves[typeof(MidQuote)], ccyPair.CcyPair, ccyPair.GetQuote<MidQuote>().Value));
        //    if (ccyPair.ContainsQuote<BidQuote>())
        //        output.Add(typeof(BidQuote), new FxForwardCurve(domCurves[typeof(BidQuote)], foreignCurves[typeof(BidQuote)], ccyPair.CcyPair, ccyPair.GetQuote<BidQuote>().Value));
        //    if (ccyPair.ContainsQuote<AskQuote>())
        //        output.Add(typeof(AskQuote), new FxForwardCurve(domCurves[typeof(AskQuote)], foreignCurves[typeof(AskQuote)], ccyPair.CcyPair, ccyPair.GetQuote<AskQuote>().Value));

        //    return output;
        //}

        protected override IForwardForexCurve InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            var ccPairs = sheet.Data.Where(x => x is CurrencyPairSecurity).ToList();
            Require.ArgumentListCount(1, ccPairs, "sheet.Data", Error.Msg("The input sheet should contain one and only one currency pair; {0} found !", ccPairs.Count));

            var ccyPair = ccPairs.First() as CurrencyPairSecurity;

            if (ccyPair.ContainsQuote<Q>())
            {
                if (ccyPair.CcyPair.ForeignCurrency == ccyPair.CcyPair.DomesticCurrency)
                {
                    return new FxCurveOne(sheet.SpotDate, ccyPair.CcyPair);
                }

                // Ois curves 
                var oisSheet = sheet.Data.Where(x => x is I).ToList();
                var domSheet = oisSheet.Where(x => (x as I).ReferenceCurrency == ccyPair.CcyPair.DomesticCurrency).ToList();
                var domCurves = _oisBoot.Bootstrap<Q>(new DataQuoteSheet(sheet.SpotDate, domSheet));
                var forSheet = oisSheet.Where(x => (x as I).ReferenceCurrency == ccyPair.CcyPair.ForeignCurrency).ToList();
                var foreignCurves = _oisBoot.Bootstrap<Q>(new DataQuoteSheet(sheet.SpotDate, forSheet));

                return new FxForwardCurve(domCurves, foreignCurves, ccyPair.CcyPair, ccyPair.GetQuote<Q>().Value);
            }
            else
            {
                return null; // No data
            }
        }

    }
}
