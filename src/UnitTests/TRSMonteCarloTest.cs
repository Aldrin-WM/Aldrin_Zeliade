using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using AldrinAnalytics.Pricers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class MonteCarloTest
    {
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

            int nbrAssets = 50;
            for (int i = 0; i < nbrAssets; i++)
            {
                var tickerCCY = (i < nbrAssets / 2) ? usd.Code : eur.Code;
                var divCCY = tickerCCY;
                var ticker = new SingleNameTicker(string.Format("SN{0}", i), tickerCCY, divCCY);
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
            var constRepoRate = 0.02;
            var repoMarket = new RepoMarket(asof);
            var repoBootstrapper = new RepoCurveBootstrapper();
            var repoRates = Enumerable.Range(1, 10).Select(i => RepoRate.NewMid(asof, constRepoRate, basket, new DateTime(2019, 12, 17).AddYears(i), CompoundingRateType.Simply
                 , DayCountConventions.Get(DayCountConventions.Codings.Actual360))).ToList();
            var repoSheet = new DataQuoteSheet(asof, repoRates);

            repoMarket.AddSheet(basket, repoSheet, repoBootstrapper);

            // Dividends market
            var grossDiv = 3.0;
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
                    cov[i, j] = System.Math.Exp(-0.2d * System.Math.Abs(i - j)) * sig[i] * sig[j];
                    cov[j, i] = cov[i, j];
                }
            }

            int nbrFactors = System.Math.Min(15,nbrAssets);
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
                //{ eur.Code, oisMarket.Get(eur, null, typeof(MidQuote)) }
            };

            var divCurve = new Dictionary<Type, IDividendCurve>()
            {
                {typeof(MidQuote),  divMarket.Get(basket, null, typeof(MidQuote)) }
            };

            var proc = new JointModel(asof, basket, gen, fxCurve, fwdDict, divCurve, new double[basket.Content.Count]);

            // Product
            double quotity = 10.0;
            var schedule = BusinessSchedule.NewParametric(asof.AddMonths(1), asof.AddMonths(1 + 3 * 12), Periods.Get("3M"));
            AssetLegStd leg = AssetLegStd.New(schedule, "Payer", "Receiver"
                , eur.Code, basket, quotity, 1d, "Id0");

            var p = new AssetLegProduct(leg);

            // Numeraire
            var oisEur = oisMarket.Get(eur, null, typeof(MidQuote));
            var cashNum = new CashProcess(oisEur);

            ///// MC Pricer
            var setting = new MonteCarloEngineSetting() { BlockSize = 100 };
            var pricer = MonteCarloEngine.PricerWithoutDataSource(asof, proc, cashNum, p, setting);

            var euroPrice = EuropeanPrice.EuropeanPriceWithFixedPathNumber(50000, 0.99);
            var req = new TaskRequest(euroPrice);

            pricer.Price(req);

            var priceMC = euroPrice.GetPrice(leg.PayerParty);
            var confiInterval = euroPrice.GetConfidence(leg.PayerParty);

            ///// CF Pricer
            var eqm = basket.Content.ToDictionary(x => x, x => securityMarket.Get(x.Name).GetQuote<MidQuote>().Value);

            var repoCurve = repoMarket.Get(basket, null, typeof(MidQuote));
            var fxm = fxMarket.RegisteredTicker.ToDictionary(x => new Tuple<string, string>(((CurrencyPair)x).ForeignCurrency, ((CurrencyPair)x).DomesticCurrency), x => fxMarket.Get((CurrencyPair)x, null, typeof(MidQuote)));

            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve[typeof(MidQuote)], repoCurve, fxm, null);

            var assetLegpricer = new AssetLegStdFormula(asof, fwdBasket, disc[leg.Currency], divCurve[typeof(MidQuote)], fxm, leg);

            var request = new TrsPricingRequest(PricingTask.Price, "Payer");
            assetLegpricer.Price(request);

            var priceCF = request.DirtyPrice;

            Assert.AreEqual(priceMC, priceCF, confiInterval);
        }

        [TestMethod]
        public void MonteCarloPricerVsClosedFormula()
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

            int nbrAssets = 50;
            for (int i = 0; i < nbrAssets; i++)
            {
                var tickerCCY = (i < nbrAssets / 2) ? usd.Code : eur.Code;
                var divCCY = tickerCCY;
                var ticker = new SingleNameTicker(string.Format("SN{0}", i), tickerCCY, divCCY);
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
            var eureur = new CurrencyPair(eur.Code, eur.Code);
            var oneSheet = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.0, eureur) });
            fxMarket.AddSheet(eureur, oneSheet, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));
            var usdusd = new CurrencyPair(usd.Code, usd.Code);
            var oneSheetUsd = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.0, usdusd) });
            fxMarket.AddSheet(usdusd, oneSheetUsd, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));

            // Repo
            var constRepoRate = 0.02;
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


            double BSvol = 0.2;
            double[] sig = Enumerable.Repeat(BSvol, basket.Count()).ToArray();
            double[,] cov = new double[basket.Count(), basket.Count()];
            for (int i = 0; i < cov.GetLength(0); i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    cov[i, j] = System.Math.Exp(-0.2d * System.Math.Abs(i - j));
                    cov[j, i] = cov[i, j];
                }
            }

            int nbrFactors = System.Math.Min(15, basket.Content.Count);

            var bsBasket = new BlackScholesBasket(basket.Content.Select(x => x.IdentifierName).ToArray(), sig, cov, basket, nbrFactors);
           
            // Product
            double quotity = 10.0;
            var schedule = BusinessSchedule.NewParametric(asof.AddMonths(1), asof.AddMonths(1 + 3 * 12), Periods.Get("3M"));

            AssetLegStd leg = AssetLegStd.New(schedule, "Payer", "Receiver"
                , eur.Code
                , basket, quotity, 1d, "Id0");

            var p = new AssetLegProduct(leg);

            ///// MC Pricer
            var setting = new MonteCarloEngineSetting() { BlockSize = 100 };
           

            var fixings = new JointHistoricalFixings(new HistoricalFixings(),
                new HistoricalFixings(), new HistoricalFixings(), new HistoricalFixings()
                , divMarket, new SecurityBasket[] { });

            var pricer1 = new AssetProductMCPricer(divMarket
                , repoMarket
                , oisMarket
                , new GenericMarket<RateReference, IForwardRateCurve>(asof)
                , new GenericMarket<LiborReference, IDiscountCurve<DateTime>>(asof)
                , securityMarket
                , fxMarket
                , leg
                , new CashCollateral(eur)
                , fixings
                , bsBasket
                , setting
                , 50000
                , 0.99
                , false
                , 0d
                );

            var req = new TrsPricingRequest(PricingTask.Price, "Payer");
            pricer1.Price(req);
            var priceMC = req.DirtyPrice;
            var confiInterval = req.DirtyPriceConfidence;

            ///// CF Pricer
            var eqm = basket.Content.ToDictionary(x => x, x => securityMarket.Get(x.Name).GetQuote<MidQuote>().Value);

            var repoCurve = repoMarket.Get(basket, null, typeof(MidQuote));
            var fxm = fxMarket.RegisteredTicker.ToDictionary(x => new Tuple<string, string>(((CurrencyPair)x).ForeignCurrency, ((CurrencyPair)x).DomesticCurrency), x => fxMarket.Get((CurrencyPair)x, null, typeof(MidQuote)));
            var divCurve = divMarket.Get(basket, null, typeof(MidQuote));
            var disc = oisMarket.RegisteredTicker.ToDictionary(x => (Currency)x, x => oisMarket.Get((Currency)x, null, typeof(MidQuote)));

            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve, repoCurve, fxm, null);

            var assetLegpricer = new AssetLegStdFormula(asof, fwdBasket, disc[leg.Currency], divCurve, fxm, leg);

            var request = new TrsPricingRequest(PricingTask.Price, "Payer");
            assetLegpricer.Price(request);

            var priceCF = request.DirtyPrice;

            Assert.AreEqual(priceMC, priceCF, confiInterval);
            
        }


    }

    [TestClass]
    public class AffinePriceModelTest
    {

        [TestMethod]
        public void AffinePriceModelVsClosedFormula()
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

            int nbrAssets = 50;
            for (int i = 0; i < nbrAssets; i++)
            {
                var tickerCCY = (i < nbrAssets / 2) ? usd.Code : eur.Code;
                var divCCY = tickerCCY;
                var ticker = new SingleNameTicker(string.Format("SN{0}", i), tickerCCY, divCCY);
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
            var eureur = new CurrencyPair(eur.Code, eur.Code);
            var oneSheet = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.0, eureur) });
            fxMarket.AddSheet(eureur, oneSheet, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));
            var usdusd = new CurrencyPair(usd.Code, usd.Code);
            var oneSheetUsd = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.0, usdusd) });
            fxMarket.AddSheet(usdusd, oneSheetUsd, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));

            // Repo
            var constRepoRate = 0.02;
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


            double BSvol = 0.2;
            double[] sig = Enumerable.Repeat(BSvol, basket.Count()).ToArray();
            double[,] cov = new double[basket.Count(), basket.Count()];
            for (int i = 0; i < cov.GetLength(0); i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    cov[i, j] = System.Math.Exp(-0.2d * System.Math.Abs(i - j));
                    cov[j, i] = cov[i, j];
                }
            }

            int nbrFactors = System.Math.Min(15, basket.Content.Count);

            var bsBasket = new BlackScholesBasket(basket.Content.Select(x => x.IdentifierName).ToArray(), sig, cov, basket, nbrFactors);

            // Product
            double quotity = 10.0;
            var schedule = BusinessSchedule.NewParametric(asof, asof.AddMonths(1 + 3 * 12), Periods.Get("3M"));

            var legAssetReset = AssetLegReset.AssetLegPriceReset(schedule, schedule, "Payer", "Receiver"
                , eur.Code
                , basket, quotity, 0d, 1d,  "Id0");
            var dayCount = DayCountConventions.Get(DayCountConventions.Codings.Actual360);

            var fixRef = new LiborReference(eur.Code, Periods.Get("3M"), dayCount);

            var floatLeg = new AssetLegFloatRate(schedule, "Receiver", "Payer"
                , eur.Code
                , basket, fixRef, 0d, dayCount, quotity, "Id0");
            var trs = new TotalReturnSwap("TRS", legAssetReset, floatLeg);

            var p = trs.ToProduct(asof, true, "Payer");           

            ///// MC Pricer
            var setting = new MonteCarloEngineSetting() { BlockSize = 100 };

            var fixings = new JointHistoricalFixings(new HistoricalFixings(),
                new HistoricalFixings(), new HistoricalFixings(), new HistoricalFixings()
                , divMarket, new SecurityBasket[] { });

            var fwdMarket = new GenericMarket<RateReference, IForwardRateCurve>(asof);
            var boot = new MyBootstrapper() { Ccy = eur.Code };
            var sheet = new DataQuoteSheet(asof);
            fwdMarket.AddSheet(fixRef, sheet, boot);

            var pricer1 = new AssetProductMCPricer(divMarket
                , repoMarket
                , oisMarket
                , fwdMarket
                , new GenericMarket<LiborReference, IDiscountCurve<DateTime>>(asof)
                , securityMarket
                , fxMarket
                , trs
                , new CashCollateral(eur)
                , fixings
                , bsBasket
                , setting
                , 50000
                , 0.99
                , false
                , 0d
                );

            var req = new TrsPricingRequest(PricingTask.Price, "Payer");
            var proc = pricer1.Process(trs, req, true) as AffinePriceModel;

            // Unsigned calculation, does not depend on the pricing reference.
            var legPrice = quotity * System.Math.Abs(proc.ComputeAssetLeg(asof, spots));
            var legFltPrice = quotity * System.Math.Abs(proc.ComputeFloatRateLeg(asof, spots));

            ///// CF Pricer Asset leg
            AssetLegStd legStd = AssetLegStd.New(schedule, "Payer", "Receiver"
               , eur.Code, basket, quotity, 1d, "Id0");
            
            var eqm = basket.Content.ToDictionary(x => x, x => securityMarket.Get(x.Name).GetQuote<MidQuote>().Value);

            var repoCurve = repoMarket.Get(basket, null, typeof(MidQuote));
            var fxm = fxMarket.RegisteredTicker.ToDictionary(x => new Tuple<string, string>(((CurrencyPair)x).ForeignCurrency, ((CurrencyPair)x).DomesticCurrency), x => fxMarket.Get((CurrencyPair)x, null, typeof(MidQuote)));
            var divCurve = divMarket.Get(basket, null, typeof(MidQuote));
            var disc = oisMarket.RegisteredTicker.ToDictionary(x => (Currency)x, x => oisMarket.Get((Currency)x, null, typeof(MidQuote)));

            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve, repoCurve, fxm, null);

            var assetLegpricer = new AssetLegStdFormula(asof, fwdBasket, disc[legStd.Currency], divCurve, fxm, legStd);

            var request = new TrsPricingRequest(PricingTask.Price, "Payer");
            assetLegpricer.Price(request);

            var priceCF = System.Math.Abs(request.DirtyPrice);

            Assert.AreEqual( System.Math.Abs((legPrice-priceCF) / priceCF), 0d, 0.0001);

            var floatLegPricer = new AssetLegFloatRateFormula(asof, fwdBasket, disc[legStd.Currency], boot.Bootstrap<MidQuote>(sheet), floatLeg);
            var requestFlt = new TrsPricingRequest(PricingTask.Price, "Payer");
            floatLegPricer.Price(requestFlt);

            var priceCFFlt = System.Math.Abs(requestFlt.DirtyPrice);
            Assert.AreEqual(System.Math.Abs((legFltPrice- priceCFFlt)/ priceCFFlt), 0d, 0.002);

        }


    }

    class MyBootstrapper : IGenericBootstrapper<IForwardRateCurve>
    {
        public string Ccy { get; set; }
        public Dictionary<Type, IForwardRateCurve> Bootstrap(DataQuoteSheet sheet)
        {
            var curve = Bootstrap<MidQuote>(sheet);
            return new Dictionary<Type, IForwardRateCurve>() { { typeof(MidQuote), curve } };
        }

        public IForwardRateCurve Bootstrap<Q>(DataQuoteSheet sheet) where Q : IDataQuote
        {
            return DiscountCurveBootstrapper<DateTime>.FlatRateCurve(sheet.SpotDate, Ccy, 0.01, CompoundingRateType.Annually) as IForwardRateCurve;
        }
    }
}
