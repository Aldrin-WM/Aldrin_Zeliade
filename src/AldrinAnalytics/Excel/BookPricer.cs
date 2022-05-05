using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using AldrinAnalytics.Pricers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Common;
using Zeliade.Finance.Common.Pricer.MonteCarlo;
using System.Diagnostics;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class BookPricingOutput
    {
        private const string XllName = "BookPricingOutput";

        private readonly Book _book;
        private readonly Dictionary<string, TrsPricingRequest> _resultsByLeg;
        private readonly Dictionary<string, TrsPricingRequest> _resultsByInstrument;
        private readonly Dictionary<string, string> _errors;
        private readonly Dictionary<string, double> _pricingTime;
        
        public BookPricingOutput(Book book)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            _resultsByLeg = new Dictionary<string, TrsPricingRequest>();
            _resultsByInstrument = new Dictionary<string, TrsPricingRequest>();
            _errors = new Dictionary<string, string>();
            _pricingTime = new Dictionary<string, double>();
        }

        public string[] LegKeys { get { return _resultsByLeg.Keys.ToArray(); } }

        [WorksheetFunction(XllName + ".Instruments")]        
        public string[] InstrumentKeys { get { return _resultsByInstrument.Keys.ToArray(); } }

        public void AddLegResult(TrsPricingRequest req)
        {
            if (_resultsByLeg.ContainsKey(req.InstrumentId))
            {
                throw new ArgumentException(string.Format("The result for leg {0} is already registred!", req.InstrumentId));
            }
            _resultsByLeg.Add(req.InstrumentId, req);
        }

        public void AddInstrumentResult(TrsPricingRequest req)
        {
            if (_resultsByInstrument.ContainsKey(req.InstrumentId))
            {
                throw new ArgumentException(string.Format("The result for leg {0} is already registred!", req.InstrumentId));
            }
            _resultsByInstrument.Add(req.InstrumentId, req);
            _errors.Add(req.InstrumentId, "OK");
        }

        public void AddError(string instId, string error)
        {
            if (_errors.ContainsKey(instId))
            {
                throw new ArgumentException(string.Format("The status for instrument {0} is already registred!", instId));
            }
            _errors.Add(instId, error);
        }
        public void AddPricingTime(string instId, double duration)
        {
            if (_pricingTime.ContainsKey(instId))
            {
                throw new ArgumentException(string.Format("The pricing time for instrument {0} is already registred!", instId));
            }
            _pricingTime.Add(instId, duration);
        }

        [WorksheetFunction(XllName + ".GetStatus")]
        public string Status(string instrument)
        {
            string output;
            if (!_errors.TryGetValue(instrument, out output))
            {
                throw new ArgumentException(string.Format("No status available for the instrument {0} !", instrument));
            }
            return output;
        }

        [WorksheetFunction(XllName + ".GetPricingTime")]
        public double PricingTime(string instrument)
        {
            double output;
            if (!_pricingTime.TryGetValue(instrument, out output))
            {
                throw new ArgumentException(string.Format("No pricing time available for the instrument {0} !", instrument));
            }
            return output;
        }

        [WorksheetFunction(XllName + ".GetLeg1")]
        public string GetLeg1(string instrument)
        {
            if (!_book.Contains(instrument))
            {
                throw new ArgumentException(string.Format("No result available for the instrument {0} !", instrument));
            }
            var product = _book.Get(instrument);
            if (product is TotalReturnSwap)
                return (product as TotalReturnSwap).Leg1.Id;
            if (product is AssetLegFloatRate)
                return (product as AssetLegFloatRate).Id;
            if (product is AssetLegStd)
                return (product as AssetLegStd).Id;

            Ensure.That(false, Error.Msg("The leg getter is not implemented for the instrument type {0} !", product.GetType()));
            return "";
        }

        [WorksheetFunction(XllName + ".GetLeg2")]
        public string GetLeg2(string instrument)
        {
            if (!_book.Contains(instrument))
            {
                throw new ArgumentException(string.Format("No result available for the instrument {0} !", instrument));
            }
            var product = _book.Get(instrument);
            if (product is TotalReturnSwap)
                return (product as TotalReturnSwap).Leg2.Id;
            if (product is AssetLegFloatRate)
                return "NONE";
            if (product is AssetLegStd)
                return "NONE";

            Ensure.That(false, Error.Msg("The leg getter is not implemented for the instrument type {0} !", product.GetType()));
            return "";
        }

        [WorksheetFunction(XllName + ".DirtyPrice")]
        public double DirtyPrice(string instrument)
        {
            TrsPricingRequest x;
            if (!_resultsByInstrument.TryGetValue(instrument, out x))
            {
                throw new ArgumentException(string.Format("No result available for the instrument {0} !", instrument));
            }
            return x.DirtyPrice;
        }

        [WorksheetFunction(XllName + ".DirtyPriceConfidence")]
        public double DirtyPriceConfidence(string instrument)
        {
            TrsPricingRequest x;
            if (!_resultsByInstrument.TryGetValue(instrument, out x))
            {
                throw new ArgumentException(string.Format("No result available for the instrument {0} !", instrument));
            }
            return x.DirtyPriceConfidence;
        }

        [WorksheetFunction(XllName + ".FairSpread")]
        public double FairSpread(string instrument)
        {
            TrsPricingRequest x;
            if (!_resultsByInstrument.TryGetValue(instrument, out x))
            {
                throw new ArgumentException(string.Format("No result available for the instrument {0} !", instrument));
            }
            return x.FairSpread;
        }

        [WorksheetFunction(XllName + ".DirtyPriceLeg")]
        public double DirtyPriceLeg(string leg)
        {
            TrsPricingRequest x;
            if (!_resultsByLeg.TryGetValue(leg, out x))
            {
                throw new ArgumentException(string.Format("No result available for the leg {0} !", leg));
            }
            return x.DirtyPrice;
        }

        [WorksheetFunction(XllName + ".DirtyPriceLegConfidence")]
        public double DirtyPriceLegConfidence(string leg)
        {
            TrsPricingRequest x;
            if (!_resultsByLeg.TryGetValue(leg, out x))
            {
                throw new ArgumentException(string.Format("No result available for the leg {0} !", leg));
            }
            return x.DirtyPriceConfidence;
        }

        [WorksheetFunction(XllName + ".RequestByInstrument")]
        public TrsPricingRequest RequestByInstrument(string instrument)
        {
            TrsPricingRequest output = null;
            if (!_resultsByInstrument.TryGetValue(instrument, out output))
            {
                throw new ArgumentException(string.Format("The requested output for instrument {0} is missing !", instrument));
            }
            return output;
        }
    }

    public class BookPricer
    {

        private const string XllName = "BookPricer";


        [WorksheetFunction(XllName + ".Price1")]
        public static BookPricingOutput Price(DateTime asof
          , Book book
          , PricingContextSet contexts
          , ModelSet models
          , PricingSettingSet settings
          , bool[] isActive
          , string[] instrument
          , string[] context
          , string[] model
          , string[] setting
          , bool[] bidAsk
          , string refParty
          , string refCurrency
          , IList<PricingTask> tasks)
        {
            Require.ArgumentNotNull(book, "book");
            Require.ArgumentNotNull(contexts, "contexts");
            Require.ArgumentNotNull(models, "models");
            Require.ArgumentNotNull(settings, "settings");
            Require.ArgumentNotNull(isActive, "isActive");
            Require.ArgumentNotNull(instrument, "instrument");
            Require.ArgumentNotNull(context, "context");
            Require.ArgumentNotNull(model, "model");
            Require.ArgumentNotNull(setting, "setting");
            Require.ArgumentNotNull(bidAsk, "bidAsk");
            Require.ArgumentNotNull(tasks, "tasks");
            Require.ArgumentNotNullOrEmpty(refParty, "refParty");
            Require.ArgumentNotNull(refCurrency, "refCurrency");

            Require.Argument(instrument.Length <= context.Length, "context", Error.Msg("The length of vector 'context' {0} should be larger than the length of vector 'instrument' {1}", context.Length, instrument.Length));
            Require.Argument(instrument.Length <= isActive.Length, "isActive", Error.Msg("The length of vector 'isActive' {0} should be larger than the length of vector 'instrument' {1}", context.Length, instrument.Length));
            Require.Argument(instrument.Length <= model.Length, "model", Error.Msg("The length of vector 'model' {0} should be larger than the length of vector 'instrument' {1}", context.Length, instrument.Length));
            Require.Argument(instrument.Length <= setting.Length, "setting", Error.Msg("The length of vector 'setting' {0} should be larger than the length of vector 'instrument' {1}", context.Length, instrument.Length));
            Require.Argument(instrument.Length <= bidAsk.Length, "bidAsk", Error.Msg("The length of vector 'bidAsk' {0} should be larger than the length of vector 'instrument' {1}", context.Length, instrument.Length));

            BookPricingOutput output = new BookPricingOutput(book);
            //var bookRes = new TrsPricingRequest(tasks, refParty);
            //bookRes.Currency = refCurrency;
            //bookRes.InstrumentId = book.Id;
            //bookRes.InstrumentType = typeof(Book);

            for (int i = 0; i < instrument.Length; i++)
            {
                string instrumentId = "Unknown";
                try 
                {

                    if (!isActive[i]) continue;
                    GlobalMarket ctx = null;

                    IAssetProduct p = null;

                    if (!book.TryGet(instrument[i], out p))
                    {
                        throw new ArgumentException(string.Format("The instrument {0} is not registered in the book {1}", instrument[i], book.Id));
                    }
                    instrumentId = p.Id;

                    ICollateralScheme c = book.GetCollat(instrument[i]);

                    if (!contexts.TryGet(context[i], out ctx))
                    {
                        throw new ArgumentException(string.Format("The market configuration {0} is not registered !", context[i]));
                    }

                    ISingleTickerModel m = null;
                    var keyModel = Tuple.Create(model[i], p.Legs.First().Underlying); // BEWARE ONLY SINGLE BASKET TRADE IS SUPPORTED
                    if (!models.TryGet(keyModel, out m))
                    {
                        throw new ArgumentException(string.Format("The model configuration {0}/{1} is not registered !", model[i], p.Legs.First().Underlying.Name));
                    }
                    TrsPricingRequest result;
                    var sw = new Stopwatch();
                    sw.Start();
                    if (m != null && !(m is StaticModel))
                    {

                        IPricingSetting pSetting = null;
                        if (!settings.TryGet(setting[i], out pSetting))
                        {
                            throw new ArgumentException(string.Format("The pricing setting {0} is not registered !", setting[i]));

                        }

                        result = MonteCarloPrice(asof, p, c, ctx, m, pSetting, refParty, tasks, bidAsk[i]);

                    }
                    else
                    {
                        result = AnalyticPrice(asof, p, c, ctx, refParty, tasks, bidAsk[i]);
                    }
                    sw.Stop();
                    output.AddLegResult(result.Leg1);
                    var agg = new TrsPricingRequest(tasks, refParty);
                    agg.Currency = refCurrency;
                    agg.InstrumentId = p.Id;
                    agg.InstrumentType = p.GetType();
                    agg.Aggregate(result.Leg1, ctx.FxMarket);

                    // Special case for dirty price confidence
                    agg.DirtyPriceConfidence = result.DirtyPriceConfidence;

                    agg.SignBasketDelta = result.SignBasketDelta;

                    var trs = p as TotalReturnSwap;
                    if (trs != null)
                    {
                        output.AddLegResult(result.Leg2);
                        agg.Aggregate(result.Leg2, ctx.FxMarket);

                        var trsLeg1AsRate = trs.Leg1 as IRateLeg;
                        var trsLeg2AsRate = trs.Leg2 as IRateLeg;

                        if (trsLeg1AsRate == null && trsLeg2AsRate != null)
                        {                            
                            agg.FairSpread = (-result.Leg1.DirtyPrice - result.Leg2.FloatRateComponent) / result.Leg2.Duration;
                        }
                        else if (trsLeg1AsRate != null && trsLeg2AsRate == null)
                        {
                            agg.FairSpread = (-result.Leg2.DirtyPrice - result.Leg1.FloatRateComponent) / result.Leg1.Duration;
                        }

                    }
                    output.AddInstrumentResult(agg);
                    output.AddPricingTime(instrumentId, sw.ElapsedMilliseconds / 1000d);
                }
                catch (Exception e)
                {
                    var errorStr = string.Format("ERROR : {0}",e.ToString());
                    if (e.InnerException != null)
                        errorStr = string.Format("{0} *** INNER EXCEPTION:{1}", errorStr, e.InnerException.ToString());
                    output.AddError(instrumentId, errorStr);
                    output.AddPricingTime(instrumentId, 0d);
                }
            }

            return output;
        }

        [WorksheetFunction(XllName + ".Price")]
        public static BookPricingOutput Price(DateTime asof
            , Book instruments
            , GlobalMarket markets
            , string refParty
            , string refCurrency
            , IList<PricingTask> tasks)
        {
            BookPricingOutput output = new BookPricingOutput(instruments);
            var bookRes = new TrsPricingRequest(tasks, refParty);
            bookRes.Currency = refCurrency;
            bookRes.InstrumentId = instruments.Id;
            bookRes.InstrumentType = typeof(Book);

            foreach (var item in instruments)
            {

                var result = AnalyticPrice(asof, item.Item1, item.Item2, markets, refParty, tasks, false);

                output.AddLegResult(result.Leg1);
                var agg = new TrsPricingRequest(tasks, refParty);
                agg.Currency = refCurrency;
                agg.InstrumentId = item.Item1.Id;
                agg.InstrumentType = item.Item1.GetType();
                agg.Aggregate(result.Leg1, markets.FxMarket);
                if (result.Leg2 != null)
                {
                    output.AddLegResult(result.Leg2);
                    agg.Aggregate(result.Leg2, markets.FxMarket);

                    if (result.Leg1.InstrumentType == typeof(AssetLegStd)
                        && result.Leg2.InstrumentType == typeof(AssetLegFloatRate))
                    {
                        agg.FairSpread = (-result.Leg1.DirtyPrice - result.Leg2.FloatRateComponent) / result.Leg2.Duration;
                    }
                    else if (result.Leg1.InstrumentType == typeof(AssetLegFloatRate)
                        && result.Leg2.InstrumentType == typeof(AssetLegStd))
                    {
                        agg.FairSpread = (-result.Leg2.DirtyPrice - result.Leg1.FloatRateComponent) / result.Leg1.Duration;

                    }

                }
                output.AddInstrumentResult(agg);

            }


            return output;
        }

        private static TrsPricingRequest AnalyticPrice(DateTime asof
            , IAssetProduct product
            , ICollateralScheme collat
            , GlobalMarket markets
            , string refParty
            , IList<PricingTask> tasks
            , bool bidAsk)
        {

            if (product is TotalReturnSwap)
            {
                var swap = product as TotalReturnSwap;
                var reqLeg1 = GenericLegPrice(asof, swap.Leg1, collat, markets, refParty, tasks, bidAsk);
                var reqLeg2 = GenericLegPrice(asof, swap.Leg2, collat, markets, refParty, tasks, bidAsk);
                var output = new TrsPricingRequest(tasks, refParty);
                output.Leg1 = reqLeg1;
                output.Leg2 = reqLeg2;
                output.SignBasketDelta = reqLeg1.SignBasketDelta;
                return output;
            }
            else
            {
                var leg = product as IAssetLeg;
                var reqLeg1 = GenericLegPrice(asof, leg, collat, markets, refParty, tasks, bidAsk);
                var output = new TrsPricingRequest(tasks, refParty);
                output.Leg1 = reqLeg1;
                output.SignBasketDelta = reqLeg1.SignBasketDelta;
                return output;
            }


        }

        private static TrsPricingRequest GenericLegPrice(DateTime asof, IAssetLeg leg
            , ICollateralScheme collat
            , GlobalMarket markets, string refParty, IList<PricingTask> tasks
            , bool bidAsk)
        {
            TrsPricingRequest req = null;
            if (leg is AssetLegStd)
            {
                req = AssetLegStdPrice(asof, leg as AssetLegStd, collat, markets, refParty, tasks, bidAsk);
            }
            else if (leg is AssetLegFloatRate)
            {
                req = FloatLegStdPrice(asof, leg as AssetLegFloatRate, collat, markets, refParty, tasks, bidAsk);
            }
            else
            {
                throw new ArgumentException(string.Format("Unsupported leg type {0}", leg.GetType()));
            }
            return req;
        }

        private static TrsPricingRequest AssetLegStdPrice(DateTime asof, AssetLegStd leg
            , ICollateralScheme collat
            , GlobalMarket markets, string refParty, IList<PricingTask> tasks
            , bool bidAsk)
        {
            var req = new TrsPricingRequest(tasks, refParty);
            req.IsMidPrice = !bidAsk;

            var pricer = new AssetLegStdPricer(markets.DivMarket, markets.RepoMarket
                , markets.OisMarket, markets.ForwardRateCurveMarket, markets.LiborDiscMarket
                , markets.SingleNameMarket, markets.FxMarket, leg, collat, markets.HistoFixings);

            pricer.Price(req);
            return req;
        }

        private static TrsPricingRequest FloatLegStdPrice(DateTime asof, AssetLegFloatRate leg
            , ICollateralScheme collat
            , GlobalMarket markets, string refParty, IList<PricingTask> tasks
            , bool bidAsk)
        {
            var req = new TrsPricingRequest(tasks, refParty);
            req.IsMidPrice = !bidAsk;

            var pricer = new AssetLegFloatRatePricer(markets.DivMarket, markets.RepoMarket
                , markets.OisMarket, markets.ForwardRateCurveMarket, markets.LiborDiscMarket
                , markets.SingleNameMarket, markets.FxMarket, leg, collat, markets.HistoFixings);

            pricer.Price(req);
            return req;
        }

        private static TrsPricingRequest MonteCarloPrice(DateTime asof
            , IAssetProduct product
            , ICollateralScheme collat
            , GlobalMarket markets
            , ISingleTickerModel model
            , IPricingSetting setting
            , string refParty
            , IList<PricingTask> tasks
            , bool bidAsk)
        {

            var bs = Require.ArgumentIsInstanceOf<BlackScholesBasket>(model, "model");
            var mcs = Require.ArgumentIsInstanceOf<MonteCarloPricingSetting>(setting, "setting");

            var mcEngineSetting = new MonteCarloEngineSetting(mcs.BlockSize);

            var mcPricer = new AssetProductMCPricer(markets.DivMarket, markets.RepoMarket
                , markets.OisMarket, markets.ForwardRateCurveMarket, markets.LiborDiscMarket
                , markets.SingleNameMarket, markets.FxMarket, product, collat, markets.HistoFixings, bs, mcEngineSetting
                , mcs.PathNumber, mcs.ConfidenceLevel, mcs.TargetInterval, mcs.HalfInterval);

            var req = new TrsPricingRequest(tasks, refParty);

            req.IsMidPrice = !bidAsk;

            mcPricer.Price(req);

            return req;

        }
    }
}
