using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Pricer;
using Zeliade.Finance.Common.Product;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
    public enum PricingTask
    {
        Price, EquityDelta, RepoDelta, EpsilonDelta, OisDelta, AiDelta, FxDelta, FwdFixings, LiborDiscounting, All
    };

    public class TrsPricingRequest : IPricingRequest
    {
        private const string XllName = "TrsPricingRequest";
        public string Reference { get; set; }
        public string Currency { get; set; }        
        public List<PricingTask> ToDo { get; private set; }
        public string InstrumentId { get; set; }
        public Type InstrumentType { get; set; }

        public bool IsMidPrice { get; set; }
        public TrsPricingRequest Leg1 { get; set; }
        public TrsPricingRequest Leg2 { get; set; }

        public double DirtyPrice { get; set; }
        public double DirtyPriceConfidence { get; set; }
        public double FairSpread { get; set; }
        internal double FixRateComponent { get; set; }
        internal double FloatRateComponent { get; set; }
        internal double Duration { get; set; }
        internal double SignBasketDelta { get; set; }

        public Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>> RepoDeltas { get; private set; }
        public Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>> DivDeltas { get; private set; }
        public Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>> SecurityDeltas { get; private set; }
        public Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>> FxDeltas { get; private set; }
        public Dictionary<Currency, Dictionary<IFiniteDifferenceDelta, double>> OisDeltas { get; private set; }
        public Dictionary<RateReference, Dictionary<IFiniteDifferenceDelta, double>> FwdFixingsDeltas { get; private set; }
        public Dictionary<LiborReference, Dictionary<IFiniteDifferenceDelta, double>> LiborDiscountingDeltas { get; private set; }


        private readonly HashSet<int> _hashes;

        public TrsPricingRequest(PricingTask task, string reference)
            :this(new List<PricingTask>() { task }, reference)
        {
        }

        public TrsPricingRequest(IList<PricingTask> tasks, string reference)
        {
            ToDo = tasks.ToList();
            Reference = reference;
            RepoDeltas = new Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>>();
            DivDeltas = new Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>>();
            SecurityDeltas = new Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>>();
            FxDeltas = new Dictionary<Ticker, Dictionary<IFiniteDifferenceDelta, double>>();
            OisDeltas = new Dictionary<Currency, Dictionary<IFiniteDifferenceDelta, double>>();
            FwdFixingsDeltas = new Dictionary<RateReference, Dictionary<IFiniteDifferenceDelta, double>>();
            LiborDiscountingDeltas = new Dictionary<LiborReference, Dictionary<IFiniteDifferenceDelta, double>>();


            _hashes = new HashSet<int>();
            _hashes.Add(GetHashCode());
            DirtyPriceConfidence = 0d;
            IsMidPrice = true;
            SignBasketDelta = 1d;
        }

        public TrsPricingRequest Aggregate(TrsPricingRequest request
            , IGenericMarket<CurrencyPair, IForwardForexCurve> fxMarket)
        {
            if (_hashes.Contains(request.GetHashCode()))
            {
                throw new ArgumentException("The provided request is already aggregated in the current request instance !");
            }

            var ccyPair = new CurrencyPair(request.Currency, Currency);
            var fxspot = fxMarket.Get(ccyPair, null, typeof(MidQuote)).Spot;

            DirtyPrice += fxspot * request.DirtyPrice;

            AggregateDict(SecurityDeltas, request.SecurityDeltas, fxspot);
            AggregateDict(FxDeltas, request.FxDeltas, fxspot);
            AggregateDict(RepoDeltas, request.RepoDeltas, fxspot);
            AggregateDict(DivDeltas, request.DivDeltas, fxspot);
            AggregateDict(OisDeltas, request.OisDeltas, fxspot);
            AggregateDict(FwdFixingsDeltas, request.FwdFixingsDeltas, fxspot);
            AggregateDict(LiborDiscountingDeltas, request.LiborDiscountingDeltas, fxspot);

            return this;
        }

        private void AggregateDict<T>(Dictionary<T, Dictionary<IFiniteDifferenceDelta, double>> baseData
            , Dictionary<T, Dictionary<IFiniteDifferenceDelta, double>> toAdd
            , double fx)
        {
            foreach (var item in toAdd)
            {
                Dictionary<IFiniteDifferenceDelta, double> tmp = null;
                if (!baseData.TryGetValue(item.Key, out tmp))
                {
                    tmp = new Dictionary<IFiniteDifferenceDelta, double>();
                    baseData.Add(item.Key, tmp);
                }

                foreach (var kv in item.Value)
                {
                    if (tmp.ContainsKey(kv.Key))
                    {
                        tmp[kv.Key] += fx * kv.Value;
                    }
                    else
                    {
                        tmp.Add(kv.Key, fx * kv.Value);
                    }
                }
            }
        }

            public TrsPricingRequest AddRepoDelta(Ticker ticker, IFiniteDifferenceDelta bump, double delta)
        {
            AddGeneric(ticker, bump, delta, RepoDeltas);
            return this;
        }

        public TrsPricingRequest AddDivDelta(Ticker ticker, IFiniteDifferenceDelta bump, double delta)
        {
            AddUnGeneric(ticker, bump, delta, DivDeltas);
            return this;
        }

        public TrsPricingRequest AddEquityDelta(Ticker ticker, IFiniteDifferenceDelta bump, double delta)
        {
            AddGeneric(ticker, bump, delta, SecurityDeltas);
            return this;
        }

        public TrsPricingRequest AddFxDelta(Ticker ticker, IFiniteDifferenceDelta bump, double delta)
        {
            AddGeneric(ticker, bump, delta, FxDeltas);
            return this;
        }

        public TrsPricingRequest AddOisDelta(Currency ticker, IFiniteDifferenceDelta bump, double delta)
        {
            AddGeneric(ticker, bump, delta, OisDeltas);
            return this;
        }

        public TrsPricingRequest AddFwdFixingsDeltas(RateReference ticker, IFiniteDifferenceDelta bump, double delta)
        {
            AddGeneric(ticker, bump, delta, FwdFixingsDeltas);
            return this;
        }

        public TrsPricingRequest AddLIborDiscountDeltas(LiborReference ticker, IFiniteDifferenceDelta bump, double delta)
        {
            AddGeneric(ticker, bump, delta, LiborDiscountingDeltas);
            return this;
        }

        public TrsPricingRequest AddGeneric<K>(K ticker, IFiniteDifferenceDelta bump, double delta, Dictionary<K, Dictionary<IFiniteDifferenceDelta, double>> dict)
        {
            Dictionary<IFiniteDifferenceDelta, double> dico = null;
            if (!dict.TryGetValue(ticker, out dico))
            {
                dico = new Dictionary<IFiniteDifferenceDelta, double>();
                dict.Add(ticker, dico);
            }
            if (dico.ContainsKey(bump))
            {
                dico[bump] += delta;
            }
            else
            {
                dico.Add(bump, delta);
            }
            return this;
        }

        public TrsPricingRequest AddUnGeneric<K>(K ticker, IFiniteDifferenceDelta bump, double delta, Dictionary<K, Dictionary<IFiniteDifferenceDelta, double>> dict)
        {
            Dictionary<IFiniteDifferenceDelta, double> dico = null;
            if (!dict.TryGetValue(ticker, out dico))
            {
                dico = new Dictionary<IFiniteDifferenceDelta, double>();
                dict.Add(ticker, dico);
            }
            if (dico.ContainsKey(bump))
            {
                dico[bump] += 0;
            }
            else
            {
                dico.Add(bump, delta);
            }
            return this;
        }



        #region Smart outputs

        //[WorksheetFunction(XllName+ ".GetSecurityDeltas")]
        //public object[,] GetSecurityDeltas
        //{
        //    get
        //    {
        //        int count = SecurityDeltas.Sum(x => x.Value.Count);
        //        var output = new object[count, 3];
        //        var index = 0;
        //        foreach (var item in SecurityDeltas)
        //        {
        //            foreach (var data in item.Value)
        //            {
        //                output[index, 0] = item.Key.Name;
        //                output[index, 1] = data.Value;
        //                output[index, 2] = data.Key.ToString();
        //                ++index;
        //            }
        //        }

        //        return output;
        //    }
        //}

        [WorksheetFunction(XllName + ".GetSecurityDeltas")]
        public object[,] GetSecurityDeltas
        {
            get
            {
                return GetTickerDeltaMethod(SecurityDeltas,Math.Abs(this.DirtyPrice));
            }
        }

        [WorksheetFunction(XllName + ".GetFxDeltas")]
        public object[,] GetFxDeltas
        {
            get
            {
                return GetTickerPillarDeltaMethod(FxDeltas);
            }
        }

        [WorksheetFunction(XllName + ".GetRepoDeltas")]
        public object[,] GetRepoDeltas
        {
            get
            {
                return GetTickerPillarDeltaMethod(RepoDeltas);
            }
        }

        [WorksheetFunction(XllName + ".GetEpsilonDeltas")]
        public object[,] GetEpsilonDeltas
        {
            get
            {
                return GetTickerPillarDeltaMethod(DivDeltas);
            }
        }


        [WorksheetFunction(XllName + ".GetOisDeltas")]
        public object[,] GetOisDeltas
        {
            get
            {
                return GetTickerPillarDeltaMethod(OisDeltas);
            }
        }

        [WorksheetFunction(XllName + ".GetFwdFixingsDeltas")]
        public object[,] GetFwdFixingsDeltas
        {
            get
            {
                return GetTickerPillarDeltaMethod(FwdFixingsDeltas);
            }
        }

        [WorksheetFunction(XllName + ".GetLiborDiscountingDeltas")]
        public object[,] GetLiborDiscountingDeltas
        {
            get
            {
                return GetTickerPillarDeltaMethod(LiborDiscountingDeltas);
            }
        }

        private object[,] GetTickerDeltaMethod<T>(Dictionary<T, Dictionary<IFiniteDifferenceDelta, double>> deltas, double divisor=0d)
            where T:Symbol
        {
           
            int count = deltas.Sum(x => x.Value.Count);
            var output = new object[count, 3];
            var index = 0;
            foreach (var item in deltas)
            {
                foreach (var data in item.Value)
                {
                    double outdelta = 0;
                    if (data.Key.DeltaMethod.GetType().Name== "DerivativeBasket")
                    {
                        outdelta = ((data.Value) * SignBasketDelta + 1) * 100 * SignBasketDelta;
                    }
                    else
                    {
                        outdelta = data.Value / divisor;
                    }
                    output[index, 0] = item.Key.Name;
                    output[index, 1] = outdelta;// + SignBasketDelta*addNumber;
                    output[index, 2] = data.Key.ToString();
                    ++index;
                }
            }


            return output;            
        }

        private object[,] GetTickerPillarDeltaMethod<T>(Dictionary<T, Dictionary<IFiniteDifferenceDelta, double>> deltas)
           where T : Symbol
        {

            int count = deltas.Sum(x => x.Value.Count);
            var output = new object[count, 5];
            var index = 0;
            foreach (var item in deltas)
            {
                foreach (var data in item.Value)
                {
                    output[index, 0] = item.Key.Name;

                    output[index, 1] = "#NA";
                    ISheetBumpType refBump = data.Key.Lower.First().Value ?? data.Key.Upper.First().Value;
                    if (refBump is IHaveIInstrumentSelector)
                    {
                        var selector = (refBump as IHaveIInstrumentSelector).Selector;
                        if (selector is ISingleTypeSelector)
                        {
                            var typ = (selector as ISingleTypeSelector).SelectedType;
                            var smart = typ.Name.Split('.').Last();
                            output[index, 1] = smart;
                        }
                    }
                    if (refBump is IHaveIInstrumentSelector)
                    {
                        var selector = (refBump as IHaveIInstrumentSelector).Selector;
                        if (selector is IHavePillar)
                        {
                            var pillar = (selector as IHavePillar).Pillar;
                            output[index, 2] = pillar.ToString();
                        }
                        else if (selector is IHaveTenor)
                        {
                            var tenor = (selector as IHaveTenor).Tenor;
                            output[index, 2] = tenor.Coding;
                        }
                        else
                        {
                            output[index, 2] = "#NA";
                        }
                    }
                    output[index, 3] = data.Value;
                    output[index, 4] = data.Key.ToString();
                    ++index;
                }
            }

            return output;
        }

        #endregion

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pricing Output:");
            sb.AppendFormat("Instrument Id = {0}\n", InstrumentId);
            sb.AppendFormat("Currency = {0}\n", Currency);
            sb.AppendFormat("Reference = {0}\n", Reference);
            sb.AppendFormat("Dirty Price = {0}\n", DirtyPrice);
            sb.AppendLine("Repo Deltas:");
            DeltaDictToString(sb, "Repo curve", RepoDeltas);
            sb.AppendLine("Dividend Deltas:");
            DeltaDictToString(sb, "Div curve" , DivDeltas);
            sb.AppendLine("Security Deltas:");
            DeltaDictToString(sb, "Security", SecurityDeltas);
            sb.AppendLine("Forex Deltas:");
            DeltaDictToString(sb, "Currency pair", FxDeltas);
            sb.AppendLine("OIS Deltas:");
            DeltaDictToString(sb, "Ois curve", OisDeltas);

            return sb.ToString();
        }

        private StringBuilder DeltaDictToString<K>(StringBuilder sb, string title, Dictionary<K, Dictionary<IFiniteDifferenceDelta, double>> dico)
        {
            foreach (var kv in dico)
            {
                sb.AppendFormat("\t {0} = {1}\n", title, kv.Key);
                foreach (var item in kv.Value)
                {
                    sb.AppendFormat("\t\t {0} = {1}\n", item.Key, item.Value);
                }
            }
            return sb;
        }
    }
}
