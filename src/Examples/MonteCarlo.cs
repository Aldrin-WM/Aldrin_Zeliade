using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Excel;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.Pricer.MonteCarlo;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;

namespace Examples
{
    public class MonteCarloExample
    {
        public static void Run()
        {
            // Asof
            DateTime asof = new DateTime(2020, 6, 15);

            var usd = new Currency("USD");
            var eur = new Currency("EUR");

            // OIS market
            var eurZcRates = Enumerable.Range(1, 10).Select(i => new ZeroCouponRateQuote(asof, asof.AddYears(i), eur.Code, Periods.Get("3M"), ReferenceTimeDayCount.Value, CompoundingRateType.Continuously).AddQuote(new MidQuote(asof, 0.005)));
            var eurSheet = new DataQuoteSheet(asof, eurZcRates);

            var usdZcRates = Enumerable.Range(1, 10).Select(i => new ZeroCouponRateQuote(asof, asof.AddYears(i), usd.Code, Periods.Get("3M"), ReferenceTimeDayCount.Value, CompoundingRateType.Continuously).AddQuote(new MidQuote(asof, 0.007)));
            var usdSheet = new DataQuoteSheet(asof, usdZcRates);

            var oisMarket = new GenericMarket<Currency, IDiscountCurve<DateTime>>(asof);
            var oisBoot = new DiscountCurveBootstrapperFromZcRate(Periods.Get("3M"));
            oisMarket.AddSheet(eur, eurSheet, oisBoot);
            oisMarket.AddSheet(usd, usdSheet, oisBoot);
            oisMarket.SetBump(eur, new FlatBump<IInstrument>(0.01, -0.01, QuoteBumpType.Relative, new Derivative()));

            // Mono curves
            var liborDiscMarket = new GenericMarket<LiborReference, IDiscountCurve<DateTime>>(asof);

            // Forward rate market
            IDayCountFraction floatDcf = DayCountConventions.Get(DayCountConventions.Codings.Actual360);            
            var libor = new LiborReference(eur.Code, Periods.Get("3M"), floatDcf);

            var l = new List<IInstrument>();
            #region forward rate curve instruments

            //var data = new List<IInstrument>();
            //for (int i = 0; i < tenor.Length; i++)
            //{
            //    var ois = new OvernightIndexSwapQuote(asof, Periods.Get("1M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01));

            //    // TODO CHECK VALUES
            //    ois.AddQuote(new BidQuote(quoteDate, bid[i]));
            //    ois.AddQuote(new AskQuote(quoteDate, ask[i]));
            //    ois.AddQuote(new MidQuote(quoteDate, mid[i]));

            //    data.Add(ois);
            //}
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("1M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("2M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("3M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("6M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("9M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("1Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("2Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("3Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new OvernightIndexSwapQuote(asof, Periods.Get("4Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(DepositQuote.StandardQuote(asof, Periods.Get("1M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(DepositQuote.StandardQuote(asof, Periods.Get("2M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(DepositQuote.StandardQuote(asof, Periods.Get("3M"),  0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(DepositQuote.StandardQuote(asof, Periods.Get("6M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(DepositQuote.StandardQuote(asof, Periods.Get("9M"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new SwapQuote(asof, Periods.Get("1Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), Periods.Get("1Y"), Periods.Get("6M"), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new SwapQuote(asof, Periods.Get("2Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), Periods.Get("1Y"), Periods.Get("6M"), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new SwapQuote(asof, Periods.Get("3Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), Periods.Get("1Y"), Periods.Get("6M"), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new SwapQuote(asof, Periods.Get("5Y"),  0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), Periods.Get("1Y"), Periods.Get("6M"), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new SwapQuote(asof, Periods.Get("10Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), Periods.Get("1Y"), Periods.Get("6M"), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            l.Add(new SwapQuote(asof, Periods.Get("20Y"), 0, BusinessCenters.None, BusinessDayConventions.None, DayCountConventions.Get(DayCountConventions.Codings.Actual360), DayCountConventions.Get(DayCountConventions.Codings.Actual360), Periods.Get("1Y"), Periods.Get("6M"), eur.Code).AddQuote(new MidQuote(asof, 0.01)));
            #endregion

            var fwdSheet = new DataQuoteSheet(asof, l);
            var fwdMarket = new ForwardRateCurveMarket(asof);
            fwdMarket.AddSheet(libor, fwdSheet, new ForwardCurveBootstrapperCashFutSwap(new DiscountCurveBootstrapperOis()));

            // Equity market
            var securityMarket = new SingleNameMarket(asof);
            SecurityBasket basket = new SecurityBasket("BASKET", eur.Code);
            int nbrAssets = 10;
            for (int i = 0; i < nbrAssets; i++)
            {
                var ticker = new SingleNameTicker(string.Format("SN{0}", i), (i < nbrAssets/2) ? usd.Code : eur.Code, (i < nbrAssets/2) ? usd.Code : eur.Code);
                securityMarket.AddSecurity(SingleNameSecurity.NewMid(asof, 100d + i, ticker));
                basket.AddComponent(new BasketComponent() { Underlying = ticker, Weight = 0.7 });
            }

            // Fx market
            var eurusd = new CurrencyPair(eur.Code, usd.Code);
            var fxSheet = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.10, eurusd) });
            fxSheet.AddData(eurSheet, false);
            fxSheet.AddData(usdSheet, false);
            var usdeur = new CurrencyPair(usd.Code, eur.Code);
            var fxSheet1 = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1 / 1.10, usdeur) });
            fxSheet1.AddData(eurSheet, false);
            fxSheet1.AddData(usdSheet, false);

            var fxMarket = new FxCurveMarket(asof);
            fxMarket.AddSheet(eurusd, fxSheet, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));
            fxMarket.SetBump(eurusd, new FlatBump<IInstrument>(0.01, -0.01, QuoteBumpType.Absolute, new Derivative()));
            fxMarket.AddSheet(usdeur, fxSheet1, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));
            var eureur = new CurrencyPair(eur.Code, eur.Code);
            var oneSheet = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.0, eureur) });
            fxMarket.AddSheet(eureur, oneSheet, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));

            // repo
            var repoMarket = new RepoMarket(asof);
            var repoBootstrapper = new RepoCurveBootstrapper();

            var repoRates = Enumerable.Range(1, 10).Select(i => RepoRate.NewMid(asof, 0.0022, basket
                 , new DateTime(2019, 12, 17).AddYears(i), CompoundingRateType.Simply
                 , DayCountConventions.Get(DayCountConventions.Codings.Actual360))
                ).ToList();
            var repoSheet = new DataQuoteSheet(asof, repoRates);

            repoMarket.AddSheet(basket, repoSheet, repoBootstrapper);

            var bump = new FullPillarAndUnderlyingBump<RepoRate>(0.01, -0.01, QuoteBumpType.Absolute, new Derivative());
            repoMarket.SetBump(basket, bump);

            // Dividends market
            var divQuotes = Enumerable.Range(1, 10).Select(i => DividendCoarse.NewMid(asof, 3d, asof.AddYears(i)
                 , basket)).ToList();
            var allinQuotes = Enumerable.Range(1, 10).Select(i => AllInCoarse.NewMid(asof, 0.88, asof.AddYears(i)
                 , basket)).ToList();

            var divSheet = new DataQuoteSheet(asof, divQuotes.Cast<IInstrument>().Concat(allinQuotes));
            var divMarket = new DividendMarket(asof);
            var divBootstrapper = new DividendCurveBootstrapper();
            divMarket.AddSheet(basket, divSheet, divBootstrapper);
            var bumpDiv = new FullPillarAndUnderlyingBump<DividendEstimate>(0.1, -0.1, QuoteBumpType.Absolute, new Derivative());
            var bumpAi = new FullPillarAndUnderlyingBump<AllIn>(0.01, -0.01, QuoteBumpType.Relative, new Derivative());
            divMarket.SetBump(basket, bumpDiv);
            divMarket.SetBump(basket, bumpAi);

            var global = new GlobalMarket(divMarket, repoMarket, oisMarket, fwdMarket
                , liborDiscMarket, securityMarket, fxMarket, null);

            // Product
            double quotity = 1;
            var schedule = BusinessSchedule.NewParametric(asof, asof.AddYears(5), Periods.Get("1W"));
            AssetLegStd leg = AssetLegStd.New(schedule, "Payer", "Receiver", eur.Code, basket, quotity, 1d, "assetLegStd0");

            // Product with fixed notional
            var resetSchedule = BusinessSchedule.NewParametric(asof, asof.AddYears(5), Periods.Get("1M"));

            // Product with price resseting
            double spread = 0d;
            double threshold = 0.1;
            var floatRateLeg = new AssetLegFloatRate(schedule, "Payer", "Receiver", eur.Code, basket, libor, spread, floatDcf, quotity, "floatRateLeg");

            var assetLeg = AssetLegReset.AssetLegPriceReset(schedule, resetSchedule, "Receiver", "Payer", eur.Code, basket, quotity, threshold, 1d, "assetLeg");

            var TRS = new TotalReturnSwap("TRS", assetLeg, floatRateLeg);

            var collat = new CashCollateral(eur);
            var book = new Book("BOOK");
            book.AddInstrument(TRS, collat);

            var contexts = new PricingContextSet();
            contexts.Add("BASE", global);

            var settings = new PricingSettingSet();
            settings.Add("BASE", new MonteCarloPricingSetting(10, 1000, 0.99, 0d, false));

            var models = new ModelSet();

            double[] sig = Enumerable.Repeat(0.25, basket.Count()).ToArray();
            double[,] cov = new double[basket.Count(), basket.Count()];

            for (int i = 0; i < cov.GetLength(0); i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    cov[i, j] = System.Math.Exp(-3d * System.Math.Abs(i - j));// * sig[i] * sig[j];
                    cov[j, i] = cov[i, j];
                }
            }

            var bs = new BlackScholesBasket(basket.Content.Select(x => x.Name).ToArray()
                , sig, cov, basket, 3);
            models.Add("BS", bs);

            var output = BookPricer.Price(asof, book, contexts, models, settings
                , new bool[] { true }
                , new string[] { TRS.Id}
                , new string[] { "BASE"}
                , new string[] { "BS" }
                , new string[] { "BASE" }
                , new bool[] { false }
                , "Payer", eur.Code, new PricingTask[] { PricingTask.Price }//.All }
                );

            Console.WriteLine(output.DirtyPrice(TRS.Id));
            Console.WriteLine(output.FairSpread(TRS.Id));
        }

    }
}
