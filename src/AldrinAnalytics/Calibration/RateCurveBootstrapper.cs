using System;
using System.Collections.Generic;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using System.Linq;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    public class DiscountCurveBootstrapperFromZcRate : BidAskBootstrapper<IDiscountCurve<DateTime>>
    {
        private readonly IPeriod _referenceTenor;

        private const string XllName = "DiscountCurveBootstrapperFromZcRate";

        [WorksheetFunction(XllName + ".New")]
        public DiscountCurveBootstrapperFromZcRate(IPeriod referenceTenor)
        {
            _referenceTenor = referenceTenor ?? throw new ArgumentNullException(nameof(referenceTenor));
        }
              
        protected override IDiscountCurve<DateTime> InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            IDiscountCurve<DateTime> zc = null;
            var firstZc = sheet.Data.First() as ZeroCouponRateQuote;
            try
            {
                zc = DiscountCurveBootstrapper<DateTime>.FromZcRateSheet<Q>(sheet, firstZc.Currency, _referenceTenor);
            }
            catch (Exception e)
            { }
            return zc;
        }
    }

    public class DiscountCurveBootstrapperOis : BidAskBootstrapper<IDiscountCurve<DateTime>>
    {
        private const string XllName = "DiscountCurveBootstrapperOis";

        [WorksheetFunction(XllName + ".New")]
        public DiscountCurveBootstrapperOis()
        { }

        protected override IDiscountCurve<DateTime> InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            IDiscountCurve<DateTime> zc = null;
            try
            {
                var firstQuote = sheet.Data.First() as OvernightIndexSwapQuote;
                Require.ArgumentNotNull(firstQuote, "firstQuote"); // TODO MESSAGE
                zc = DiscountCurveBootstrapper<DateTime>.MultiFromOis<Q>(firstQuote.Currency, sheet);
            }
            catch (Exception e)
            {
                // TODO LOG EXCEPTION
            }
            return zc;
        }
    }

    /// <summary>
    /// Ois curve as IForwardRateCurve
    /// </summary>
    public class FwdCurveBootstrapperOis : BidAskBootstrapper<IForwardRateCurve>
    {
        private readonly DiscountCurveBootstrapperOis _boot;

        private const string XllName = "FwdCurveBootstrapperOis";

        [WorksheetFunction(XllName + ".New")]
        public FwdCurveBootstrapperOis()
        {
            _boot = new DiscountCurveBootstrapperOis();
        }              

        protected override IForwardRateCurve InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            return _boot.Bootstrap<Q>(sheet) as IForwardRateCurve;
        }
    }

    public class ForwardCurveBootstrapperCashFutSwap : BidAskBootstrapper<IForwardRateCurve>
    {

        private const string XllName = "ForwardCurveBootstrapperCashFutSwap";

        private readonly IGenericBootstrapper<IDiscountCurve<DateTime>> _discBoot;

        [WorksheetFunction(XllName + ".New")]
        public ForwardCurveBootstrapperCashFutSwap(IGenericBootstrapper<IDiscountCurve<DateTime>> discBoot)
        {
            _discBoot = discBoot ?? throw new ArgumentNullException(nameof(discBoot));
        }

        protected override IForwardRateCurve InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            Currency ccy = new Currency((sheet.Data.First() as RateInstrument).ReferenceCurrency);

            var oisSheetList = sheet.Data.Where(x => x is OvernightIndexSwapQuote).ToList();
            var oisSheet = new DataQuoteSheet(sheet.SpotDate, oisSheetList);
            var discountCurve = _discBoot.Bootstrap<Q>(oisSheet);

            ZeroCouponRateCurve fwd = null;
            try
            {
                var depfraswaps = sheet.Data.Where(x => x is DepositQuote || x is FraQuote || x is SwapQuote).ToList();
                var lastSwap = depfraswaps.Last() as SwapQuote; // TODO CHECK

                IPeriod reference = lastSwap.FloatingPaymentfrequency;
                fwd = XiborMultiCurve.BuildFromDepositFraSwaps<Q>(reference, ccy.Code, discountCurve, sheet, null, null, null, typeof(IForwardRateCurve), usePermissive:true);
                fwd.ReferenceTenor = reference;
            }
            catch (Exception e)
            {
                // TODO LOG EXCEPTION
            }
            return fwd;
        }
    }

    public class LiborCurveAsDiscount : BidAskBootstrapper<IDiscountCurve<DateTime>>
    {

        private const string XllName = "LiborAsDiscount";

        private readonly ForwardCurveBootstrapperCashFutSwap _boot;

        [WorksheetFunction(XllName + ".New")]
        public LiborCurveAsDiscount(ForwardCurveBootstrapperCashFutSwap boot)
        {
            _boot = boot ?? throw new ArgumentNullException(nameof(boot));
        }

        protected override IDiscountCurve<DateTime> InternalBootstrap<Q>(DataQuoteSheet sheet)
        {
            return _boot.Bootstrap<Q>(sheet) as IDiscountCurve<DateTime>;
        }
    }


}
