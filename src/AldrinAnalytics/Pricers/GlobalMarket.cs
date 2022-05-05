using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using AldrinAnalytics.Models;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
    public class GlobalMarket
    {

        private const string XllName = "GlobalMarket";

        public IDividendMarket DivMarket { get; private set; }
        public IGenericMarket<Ticker, IRepoCurve> RepoMarket { get; private set; }
        public IGenericMarket<Currency, IDiscountCurve<DateTime>> OisMarket { get; private set; }
        public IGenericMarket<RateReference, IForwardRateCurve> ForwardRateCurveMarket { get; private set; }
        public IGenericMarket<LiborReference, IDiscountCurve<DateTime>> LiborDiscMarket { get; private set; }
        public IGenericMarket<SingleNameTicker, SingleNameSecurity> SingleNameMarket { get; private set; }
        public IGenericMarket<CurrencyPair, IForwardForexCurve> FxMarket { get; private set; }
        public JointHistoricalFixings HistoFixings { get; private set; }

        [WorksheetFunction(XllName + ".New")]
        public GlobalMarket(IDividendMarket divMarket
            , IGenericMarket<Ticker, IRepoCurve> repoMarket
            , IGenericMarket<Currency, IDiscountCurve<DateTime>> oisMarket
            , IGenericMarket<RateReference, IForwardRateCurve> fwdMarket
            , IGenericMarket<LiborReference, IDiscountCurve<DateTime>> liborDiscMarket
            , IGenericMarket<SingleNameTicker, SingleNameSecurity> singleNameMarket
            , IGenericMarket<CurrencyPair, IForwardForexCurve> fxMarket
            , JointHistoricalFixings histoFixings
            )
        {
            DivMarket = divMarket ?? throw new ArgumentNullException(nameof(divMarket));
            RepoMarket = repoMarket ?? throw new ArgumentNullException(nameof(repoMarket));
            OisMarket = oisMarket ?? throw new ArgumentNullException(nameof(oisMarket));
            ForwardRateCurveMarket = fwdMarket ?? throw new ArgumentNullException(nameof(fwdMarket));
            LiborDiscMarket = liborDiscMarket ?? throw new ArgumentNullException(nameof(liborDiscMarket));
            SingleNameMarket = singleNameMarket ?? throw new ArgumentNullException(nameof(singleNameMarket));
            FxMarket = fxMarket ?? throw new ArgumentNullException(nameof(fxMarket));
            HistoFixings = histoFixings;
        }

        [WorksheetFunction(XllName + ".NewWithoutHisto")]
        public GlobalMarket(IDividendMarket divMarket
            , IGenericMarket<Ticker, IRepoCurve> repoMarket
            , IGenericMarket<Currency, IDiscountCurve<DateTime>> oisMarket
            , IGenericMarket<RateReference, IForwardRateCurve> fwdMarket
            , IGenericMarket<LiborReference, IDiscountCurve<DateTime>> liborDiscMarket
            , IGenericMarket<SingleNameTicker, SingleNameSecurity> singleNameMarket
            , IGenericMarket<CurrencyPair, IForwardForexCurve> fxMarket
            )
        {
            DivMarket = divMarket ?? throw new ArgumentNullException(nameof(divMarket));
            RepoMarket = repoMarket ?? throw new ArgumentNullException(nameof(repoMarket));
            OisMarket = oisMarket ?? throw new ArgumentNullException(nameof(oisMarket));
            ForwardRateCurveMarket = fwdMarket ?? throw new ArgumentNullException(nameof(fwdMarket));
            LiborDiscMarket = liborDiscMarket ?? throw new ArgumentNullException(nameof(liborDiscMarket));
            SingleNameMarket = singleNameMarket ?? throw new ArgumentNullException(nameof(singleNameMarket));
            FxMarket = fxMarket ?? throw new ArgumentNullException(nameof(fxMarket));
            HistoFixings = null;
        }
    }
}
