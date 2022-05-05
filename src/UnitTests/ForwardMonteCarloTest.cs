using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using AldrinAnalytics.Pricers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.Pricer.MonteCarlo;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;



namespace UnitTests
{
    [TestClass]
    public class ForwardMonteCarloTest
    {
        /// <summary>
        /// Product representing a Forward. Used to perform a Monte-Carlo pricing.
        /// </summary>
        public class ForwardProduct : ProductBase
        {
            private readonly SecurityBasket _basket;

            public ForwardProduct(string payer, string receiver, DateTime effectiveDate, DateTime expiry, SecurityBasket basket)
                : base("Forward", effectiveDate, expiry)
            {
                _basket = basket;

                AddCurrency(_basket.Currency);

                AddParty(payer);
                AddParty(receiver);
                AddCallBack(expiry, AssetPayment, -1);
                Reset();
            }

            private CallBackOutput AssetPayment(CallBackArg arg)
            {
                var m = arg.Model as IJointModel;
                var output = new CallBackOutput(1);

                double payoff = 0d;
                var comps = _basket.Components;
                for (int i = 0; i < comps.Count; i++)
                {
                    var ccyStock = comps[i].Underlying.Currency.Code;
                    var ccyPair = Tuple.Create(ccyStock, _basket.Currency.Code);
                    var fx = m.FxValue(ccyPair, typeof(MidQuote));
                    payoff += comps[i].Weight * m.Value[i] * fx;
                }

                output.AddPayment(Parties[0], Parties[1], _basket.Currency.Code, payoff, "AssetPerf", false);

                return output;
            }
        }

        /// <summary>
        /// Compare the Forward pricing formula with the Monte-Carlo pricing.
        /// </summary>
        [TestMethod]
        public void MonteCarloVsClosedFormula()
        {
            MarketConventionsFactory.DefaultConfiguration();

            // Asof
            DateTime asof = new DateTime(2020, 6, 15);

            var usd = new Currency("USD");
            var eur = new Currency("EUR");

            // OIS market
            var constEurZcRate = 0.01;
            var constUSDZcRate = 0.015;
            var eurZcRates = Enumerable.Range(1, 10).Select(i => new ZeroCouponRateQuote(asof, asof.AddYears(i), eur.Code, Periods.Get("3M"), ReferenceTimeDayCount.Value, CompoundingRateType.Continuously).AddQuote(new MidQuote(asof, constEurZcRate)));
            var eurSheet = new DataQuoteSheet(asof, eurZcRates);
            var usdZcRates = Enumerable.Range(1, 10).Select(i => new ZeroCouponRateQuote(asof, asof.AddYears(i), usd.Code, Periods.Get("3M"), ReferenceTimeDayCount.Value, CompoundingRateType.Continuously).AddQuote(new MidQuote(asof, constUSDZcRate)));
            var usdSheet = new DataQuoteSheet(asof, usdZcRates);
            var oisMarket = new GenericMarket<Currency, IDiscountCurve<DateTime>>(asof);
            var oisBoot = new DiscountCurveBootstrapperFromZcRate(Periods.Get("3M"));
            oisMarket.AddSheet(eur, eurSheet, oisBoot);
            oisMarket.AddSheet(usd, usdSheet, oisBoot);

            // Equity market
            var securityMarket = new SingleNameMarket(asof);
            SecurityBasket basket = new SecurityBasket("BASKET", eur.Code);

            int nbrAssets = 2;
            for (int i = 0; i < nbrAssets; i++)
            {
                var ticker = new SingleNameTicker(string.Format("SN{0}", i), (i < nbrAssets / 2) ? usd.Code : eur.Code, (i < nbrAssets / 2) ? usd.Code : eur.Code); //eur.Code : usd.Code
                securityMarket.AddSecurity(SingleNameSecurity.NewMid(asof, 100d + i, ticker));
                basket.AddComponent(new BasketComponent() { Underlying = ticker, Weight = 1.0 });
            }

            // Fx market
            double eurUsd = 1.5;

            var eurusd = new CurrencyPair(usd.Code, eur.Code);
            var fxSheet = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, eurUsd, eurusd) });
            fxSheet.AddData(eurSheet, false);
            fxSheet.AddData(usdSheet, false);
            var usdeur = new CurrencyPair(eur.Code, usd.Code);
            var fxSheet1 = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1 / eurUsd, usdeur) });
            fxSheet1.AddData(eurSheet, false);
            fxSheet1.AddData(usdSheet, false);

            var fxMarket = new FxCurveMarket(asof);
            fxMarket.AddSheet(eurusd, fxSheet, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));
            fxMarket.AddSheet(usdeur, fxSheet1, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));

            // Repo
            var constRepoRate = 0.000;
            var repoMarket = new RepoMarket(asof);
            var repoBootstrapper = new RepoCurveBootstrapper();
            var repoRates = Enumerable.Range(1, 10).Select(i => RepoRate.NewMid(asof, constRepoRate, basket, new DateTime(2019, 12, 17).AddYears(i), CompoundingRateType.Simply
                 , DayCountConventions.Get(DayCountConventions.Codings.Actual360))).ToList();
            var repoSheet = new DataQuoteSheet(asof, repoRates);

            repoMarket.AddSheet(basket, repoSheet, repoBootstrapper);

            // Dividends market
            var grossDiv = 0.0;
            var divQuotes = Enumerable.Range(1, 10).Select(i => DividendCoarse.NewMid(asof, grossDiv, asof.AddYears(i), basket)).ToList();
            var allinQuotes = Enumerable.Range(1, 10).Select(i => AllInCoarse.NewMid(asof, 0.88, asof.AddYears(i), basket)).ToList();

            var divSheet = new DataQuoteSheet(asof, divQuotes.Cast<IInstrument>().Concat(allinQuotes));
            var divMarket = new DividendMarket(asof);
            var divBootstrapper = new DividendCurveBootstrapper();
            divMarket.AddSheet(basket, divSheet, divBootstrapper);

            // Monte Carlo pricer
            var spots = basket.Content.Select(x => securityMarket.Get(x.Name).GetQuote<MidQuote>().Value).ToArray();

            var longStep = new LongStepSimulationDateFactory();
            var initializer = new ConstantInitializer<double[]>(spots);
            var fxCurve = new Dictionary<Tuple<string, string>, IForwardForexCurve>()
            {
                {Tuple.Create(eur.Code, usd.Code), fxMarket.Get(eurusd, null, typeof(MidQuote)) },
                {Tuple.Create(usd.Code, eur.Code), fxMarket.Get(usdeur, null, typeof(MidQuote)) },
                {Tuple.Create(usd.Code, usd.Code), new FxCurveOne(asof, new CurrencyPair(usd.Code, usd.Code)) },
                {Tuple.Create(eur.Code, eur.Code), new FxCurveOne(asof, new CurrencyPair(eur.Code, eur.Code)) },
            };

            double BSvol = 0.2;
            double[] sig = Enumerable.Repeat(BSvol, basket.Count()).ToArray();
            double[,] cov = new double[basket.Count(), basket.Count()];

            for (int i = 0; i < cov.GetLength(0); i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    cov[i, j] = System.Math.Exp(-0.2 * System.Math.Abs(i - j)) * sig[i] * sig[j];
                    cov[j, i] = cov[i, j];
                }
            }

            int nbrFactors = nbrAssets;
            var disc = oisMarket.RegisteredTicker.ToDictionary(x => (Currency)x, x => oisMarket.Get((Currency)x, null, typeof(MidQuote)));

            var gen = new BlackScholesGenerator(1
                , asof
                , longStep
                , initializer
                , basket
                , divMarket.Get(basket, null, typeof(MidQuote))
                , repoMarket.Get(basket, null, typeof(MidQuote))
                , disc
                , fxCurve
                , cov
                , nbrFactors);

            var fwdDict = new Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>>()
            {
                
            };

            var divCurve = new Dictionary<Type, IDividendCurve>()
            {
                {typeof(MidQuote),  divMarket.Get(basket, null, typeof(MidQuote)) }
            };

            var proc = new JointModel(asof, basket, gen, fxCurve, fwdDict, divCurve, new double[basket.Content.Count]);

            // Product
            DateTime expiry = asof.AddMonths(12);
            var forwardProduct = new ForwardProduct("Payer", "Receiver", asof, expiry, basket);

            // Numeraire
            var nullCurve = DiscountCurveBootstrapper<DateTime>.NullRateCurve(asof, eur.Code);
            var cashNum = new CashProcess(nullCurve);

            ///// MC Pricer
            var setting = new MonteCarloEngineSetting() { BlockSize = 100 };
            var pricer = MonteCarloEngine.PricerWithoutDataSource(asof, proc, cashNum, forwardProduct, setting);

            var euroPrice = EuropeanPrice.EuropeanPriceWithFixedPathNumber(200000, 0.90);
            var req = new TaskRequest(euroPrice);

            pricer.Price(req);

            var priceMC = euroPrice.GetPrice(forwardProduct.Parties[1]);
            var confiInterval = euroPrice.GetConfidence(forwardProduct.Parties[1]);

            ///// CF Pricer
            var eqm = basket.Content.ToDictionary(x => x, x => securityMarket.Get(x.Name).GetQuote<MidQuote>().Value);

            var repoCurve = repoMarket.Get(basket, null, typeof(MidQuote));
            var fxm = fxMarket.RegisteredTicker.ToDictionary(x => new Tuple<string, string>(((CurrencyPair)x).ForeignCurrency, ((CurrencyPair)x).DomesticCurrency), x => fxMarket.Get((CurrencyPair)x, null, typeof(MidQuote)));

            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve[typeof(MidQuote)], repoCurve, fxm, null);

            var priceCF = fwdBasket.Forward(expiry);

            Assert.IsTrue(Math.Abs(priceCF - priceMC) < confiInterval);
        }


    }
}
