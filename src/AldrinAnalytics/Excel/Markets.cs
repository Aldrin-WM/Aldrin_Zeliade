using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Common;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class Markets
    {

        private const string XllName = "Markets";

        [WorksheetFunction(XllName + ".AddDividendSheet")]
        public static IGenericMarketSetter<Ticker, IDividendCurve> AddDividendSheet(IGenericMarketSetter<Ticker, IDividendCurve> divMarket
            , string basketName
            , DataQuoteSheet divEstimates
            , DataQuoteSheet divRough
            , string divProxy
            , BasketSet basketSet
            , IGenericBootstrapper<IDividendCurve> bootstrapper)
        {
            var bProxy = basketSet.GetBasket(divProxy);

            List<IInstrument> data = new List<IInstrument>();
            foreach (var item in bProxy.Content)
            {
                var estimates = divEstimates.Data.Where(x => {
                    var divEst = x as DividendEstimate;
                    return divEst != null && divEst.Underlying.Equals(item);
                }
                    );
                data.AddRange(estimates);

                var allIns = divEstimates.Data.Where(x => {
                    var allIn = x as AllIn;
                    return allIn != null && allIn.Underlying.Equals(item);
                }
                );
                data.AddRange(allIns);
            }

            var roughDivs = divRough.Data.Where(x => {
                var coarse = x as DividendCoarse;
                return coarse != null && coarse.Underlying.Equals(bProxy);
            });
            data.AddRange(roughDivs);
            var roughAllIns = divRough.Data.Where(x => {
                var coarse = x as AllInCoarse;
                return coarse != null && coarse.Underlying.Equals(bProxy);
            });
            data.AddRange(roughAllIns);
            var sheet = new DataQuoteSheet(divEstimates.SpotDate, data);

            var b = basketSet.GetBasket(basketName);
            divMarket.AddSheet(b, sheet, bootstrapper);

            return divMarket;
        }

        [WorksheetFunction(XllName + ".NewFxCurveMarket")]
        public static FxCurveMarket NewFxCurveMarket(DataQuoteSheet sheet
            , DataSheetContainer oisSheets
            , IGenericBootstrapper<IDiscountCurve<DateTime>> oisBoot)
        {
            var fxMarket = new FxCurveMarket(sheet.SpotDate);

            var fxBoot = new FxCurveBootstrapper<OvernightIndexSwapQuote>(oisBoot);

            var ccySet = new HashSet<string>();

            foreach (var item in sheet.Data)
            {
                var itemAsCcyPair = item as CurrencyPairSecurity;
                Ensure.That(itemAsCcyPair != null, Error.Msg("Got an instrument of type {0} but only {1} is allowed for fx market construction !", item.GetType(), typeof(CurrencyPairSecurity)));
                var domCcy = itemAsCcyPair.CcyPair.DomesticCurrency;
                var forCcy = itemAsCcyPair.CcyPair.ForeignCurrency;
                var locSheet = new DataQuoteSheet(sheet.SpotDate, new IInstrument[] { item });

                locSheet.AddData(oisSheets.Get(new Currency(domCcy)), false);
                locSheet.AddData(oisSheets.Get(new Currency(forCcy)), false);

                var ccyPair = new CurrencyPair(domCcy, forCcy);

                fxMarket.AddSheet(ccyPair, locSheet, fxBoot);

                if (!ccySet.Contains(domCcy))
                    ccySet.Add(domCcy);

                if (!ccySet.Contains(forCcy))
                    ccySet.Add(forCcy);
            }

            // Completing with trivial pairs if needed
            foreach (var ccy in ccySet)
            {
                var ccyPair = new CurrencyPair(ccy,ccy);
                if (fxMarket.Contains(ccyPair))
                    continue;

                var item = CurrencyPairSecurity.New(sheet.SpotDate, 1d, 1d, 1d, ccyPair);
                var locSheet = new DataQuoteSheet(sheet.SpotDate, new IInstrument[] { item });
                fxMarket.AddSheet(ccyPair, locSheet, fxBoot);
            }


            return fxMarket;
        }

        [WorksheetFunction(XllName + ".NewOisCurveMarket")]
        public static OisCurveMarket NewOisCurveMarket(DataSheetContainer sheets
            , IGenericBootstrapper<IDiscountCurve<DateTime>> bootstraper)
        {
            var mkt = new OisCurveMarket(sheets.Asof);

            foreach (var item in sheets)
            {
                var itemAsCcy = (Currency)item.Item1;
                Ensure.NotNull(itemAsCcy, Error.Msg("An input sheet is registered under a symbol of type {0} : should be {1} !", item.Item1.GetType(), typeof(Currency)));
                mkt.AddSheet(itemAsCcy, item.Item2, bootstraper);
            }

            return mkt;
        }

        [WorksheetFunction(XllName + ".NewSingleNameMarket")]
        public static SingleNameMarket NewSingleNameMarket(DataQuoteSheet sheet)
        {
            var mkt = new SingleNameMarket(sheet.SpotDate);
            foreach (var item in sheet)
            {
                var itemAsSn = item as SingleNameSecurity;
                Ensure.NotNull(itemAsSn, Error.Msg("An input sheet is registered under a symbol of type {0} : should be {1} !", item.GetType(), typeof(SingleNameSecurity)));
                mkt.AddSecurity(itemAsSn);
            }
            return mkt;
        }

        [WorksheetFunction(XllName + ".NewRepoMarket")]
        public static RepoMarket NewRepoMarket(DataSheetContainer sheets
            , IGenericBootstrapper<IRepoCurve> bootstraper)
        {
            var mkt = new RepoMarket(sheets.Asof);

            foreach (var item in sheets)
            {
                var itemAsTicker = (Ticker)item.Item1;
                Ensure.NotNull(itemAsTicker, Error.Msg("An input sheet is registered under a symbol of type {0} : should derive from {1} !", item.Item1.GetType(), typeof(Ticker)));
                mkt.AddSheet(itemAsTicker, item.Item2, bootstraper);
            }

            return mkt;
        }

        [WorksheetFunction(XllName + ".NewForwardRateCurveMarket")]
        public static ForwardRateCurveMarket NewForwardRateCurveMarket(DataSheetContainer oisSheets
            , IGenericBootstrapper<IForwardRateCurve> oisBootstrapper
            , DataSheetContainer swapSheets
            , IGenericBootstrapper<IForwardRateCurve> cashFutSwapBootstrapper
            )
        {
            Require.ArgumentNotNull(oisSheets, nameof(oisSheets));
            Require.ArgumentNotNull(swapSheets, nameof(swapSheets));
            Require.ArgumentNotNull(oisBootstrapper, nameof(oisBootstrapper));
            Require.ArgumentNotNull(cashFutSwapBootstrapper, nameof(cashFutSwapBootstrapper));
            
            var mkt = new ForwardRateCurveMarket(swapSheets.Asof);

            foreach (var item in oisSheets)
            {
                var itemAsCcy = (Currency)item.Item1;
                Ensure.NotNull(itemAsCcy, Error.Msg("An input sheet is registered under a symbol of type {0} : should be {1} !", item.Item1.GetType(), typeof(Currency)));
                
                var fltDayCount = (item.Item2.First() as OvernightIndexSwapQuote).FloatingDayCountConvention; 

                var ois = new OisReference(itemAsCcy.Code, fltDayCount);
                mkt.AddSheet(ois, item.Item2, oisBootstrapper);
            }

            foreach (var item in swapSheets)
            {
                var itemAsCcy = (Currency)item.Item1;
                Ensure.NotNull(itemAsCcy, Error.Msg("An input sheet is registered under a symbol of type {0} : should be {1} !", item.Item1.GetType(), typeof(Currency)));

                // Adding ois sheet
                var baseSheet = new DataQuoteSheet(item.Item2.SpotDate);
                baseSheet.AddData(item.Item2, false); // TODO CHECK COPY
                var oisSheet = oisSheets.Get(item.Item1);
                baseSheet.AddData(oisSheet, false); // TODO CHECK COPY
                var swaps = item.Item2.Data.Where(x => x is SwapQuote).ToList();
                var tenor = (swaps.First() as SwapQuote).FloatingPaymentfrequency; 
                var fltDayCount = (swaps.First() as SwapQuote).FloatingDayCountConvention; 

                var libor = new LiborReference(itemAsCcy.Code, tenor, fltDayCount);
                mkt.AddSheet(libor, baseSheet, cashFutSwapBootstrapper);
            }

            return mkt;
        }

        [WorksheetFunction(XllName + ".NewLiborDiscountingMarket")]
        public static GenericMarket<LiborReference, IDiscountCurve<DateTime>> NewLiborDiscountingMarket(DataSheetContainer oisSheets
            , DataSheetContainer swapSheets
            , LiborCurveAsDiscount cashFutSwapBootstrapper)
        {

            Require.ArgumentNotNull(oisSheets, nameof(oisSheets));
            Require.ArgumentNotNull(swapSheets, nameof(swapSheets));
            Require.ArgumentNotNull(cashFutSwapBootstrapper, nameof(cashFutSwapBootstrapper));

            var mkt = new GenericMarket<LiborReference, IDiscountCurve<DateTime>>(swapSheets.Asof);            

            foreach (var item in swapSheets)
            {
                var itemAsCcy = (Currency)item.Item1;
                Ensure.NotNull(itemAsCcy, Error.Msg("An input sheet is registered under a symbol of type {0} : should be {1} !", item.Item1.GetType(), typeof(Currency)));

                // Adding ois sheet
                var baseSheet = new DataQuoteSheet(item.Item2.SpotDate);
                baseSheet.AddData(item.Item2, false); // TODO CHECK COPY
                var oisSheet = oisSheets.Get(item.Item1);
                baseSheet.AddData(oisSheet, false); // TODO CHECK COPY
                var swaps = item.Item2.Data.Where(x => x is SwapQuote).ToList();
                var tenor = (swaps.First() as SwapQuote).FloatingPaymentfrequency; 
                var fltDayCount = (swaps.First() as SwapQuote).FloatingDayCountConvention; 

                var libor = new LiborReference(itemAsCcy.Code, tenor, fltDayCount);
                mkt.AddSheet(libor, baseSheet, cashFutSwapBootstrapper);
            }

            return mkt;
        }

    }
}
