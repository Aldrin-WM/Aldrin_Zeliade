using System;
using System.Linq;
using System.Collections.Generic;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Pricer;
using Zeliade.Finance.Mrc;
using Zeliade.Finance.Common.Product;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    public interface IHaveBump
    {
        double BumpValue
        {
            get;
        }
    }

    public interface IInstrumentSelector
    {
        bool Select(IInstrument item);
    }

    public interface IHaveIInstrumentSelector
    {
        IInstrumentSelector Selector { get; }
    }

    public interface ISingleTypeSelector : IInstrumentSelector
    {
        Type SelectedType { get; }
    }

    public interface ITypedGetter<K>
    {
        K Get(Type t);
    }

    public class ZeroDistance : ITypedGetter<double>
    {
        public double Get(Type t)
        {
            return 0d;
        }
    }

    public class DictDistance : ITypedGetter<double>
    {
        private readonly Dictionary<Type, double> _data = new Dictionary<Type, double>();

        public double Get(Type t)
        {
            double output = 0;
            _data.TryGetValue(t, out output);
            return output;
        }

        public void AddOrUpdate(Type t, double value)
        {
            if (_data.ContainsKey(t))
                _data[t] = value;
            else
                _data.Add(t, value);
        }
    }

    public interface ISheetBumpType
    {
        Tuple<DataQuoteSheet, ITypedGetter<double>> Bump(DataQuoteSheet baseSheet);
    }

    #region Instrument selectors

    public class AllSelector<T> : ISingleTypeSelector where T: IInstrument
    {
        public Type SelectedType { get { return typeof(T); } }

        public bool Select(IInstrument item)
        {
            return item is T;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
                return obj.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return typeof(AllSelector<T>).FullName.GetHashCode();
        }

        public override string ToString()
        {

            return string.Format("{0}<{1}>", typeof(AllSelector<T>).Name, typeof(T).Name);
        }
    }

    public class TenorSelector<T> : ISingleTypeSelector, IHaveTenor where T : IInstrument
    {
        public IPeriod Tenor { get; private set; }

        public Type SelectedType { get { return typeof(T); } }

        public TenorSelector(IPeriod tenor)
        {
            Tenor = tenor ?? throw new ArgumentNullException(nameof(tenor));
        }

        public bool Select(IInstrument item)
        {

            if (!(item is T))
            {
                return false;
            }

            var itemAsTenor = item as IHaveTenor;
            if (itemAsTenor == null)
            {
                return false;
            }

            if (System.Math.Abs(itemAsTenor.Tenor.YearDuration - Tenor.YearDuration) < 1e-10)
            {
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return string.Format("{0}<{1}> : {2} ", GetType().Name, typeof(T).Name, Tenor);

        }

        public override int GetHashCode()
        {
            int hash = 17;
            const int p2 = 23;

            unchecked
            {
                hash = hash * p2 + Tenor.DayDuration.GetHashCode();
                hash = hash * p2 + typeof(T).FullName.GetHashCode();
            }

            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
                return obj.GetHashCode() == GetHashCode();
        }
    }

    public class PillarAndUnderlyingSelector<T> : ISingleTypeSelector, IHavePillar where T : IInstrument
    {
        public DateTime Pillar { get; private set; }
        public Ticker Underlying { get; private set; }

        public Type SelectedType { get { return typeof(T); } }

        public PillarAndUnderlyingSelector(DateTime pillar, Ticker underlying)
        {
            Pillar = pillar;
            Underlying = underlying;
        }

        public bool Select(IInstrument item)
        {
            if (!(item is T))
            {
                return false;
            }

            var itemAsPillar = item as IHavePillar;
            if (itemAsPillar == null)
            {
                return false;
            }

            var itemAsU = item as IHaveUnderlying;
            if (itemAsU == null)
            {
                return false;
            }


            if (itemAsPillar.Pillar == Pillar && itemAsU.Underlying.Equals(Underlying))
            {
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return string.Format("{0}<{1}> : {2} - {3}", GetType().Name, typeof(T).Name, Pillar, Underlying);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            const int p2 = 23;

            unchecked
            {
                hash = hash * p2 + Pillar.GetHashCode();
                hash = hash * p2 + Underlying.GetHashCode();
                hash = hash * p2 + typeof(T).FullName.GetHashCode();
            }

            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
                return obj.GetHashCode() == GetHashCode();
        }
    }

    public class PillarSelector<T> : ISingleTypeSelector, IHavePillar where T : IInstrument
    {
        public DateTime Pillar { get; private set; }

        public Type SelectedType { get { return typeof(T); } }

        public PillarSelector(DateTime pillar)
        {
            Pillar = pillar;
        }

        public bool Select(IInstrument item)
        {
            if (!(item is T))
            {
                return false;
            }

            var itemAsPillar = item as IHavePillar;
            if (itemAsPillar == null)
            {
                return false;
            }

            if (itemAsPillar.Pillar == Pillar)
            {
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return string.Format("{0}<{1}> : {2}", GetType().Name, typeof(T).Name, Pillar);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            const int p2 = 23;

            unchecked
            {
                hash = hash * p2 + Pillar.GetHashCode();
                hash = hash * p2 + typeof(T).FullName.GetHashCode();
            }

            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
                return obj.GetHashCode() == GetHashCode();
        }
    }

    #endregion

    #region Implementations of ISheetBumpType

    public enum QuoteBumpType { Absolute, Relative };

    public class BaseSheet : ISheetBumpType
    {
        public Tuple<DataQuoteSheet, ITypedGetter<double>> Bump(DataQuoteSheet baseSheet)
        {
            return Tuple.Create(baseSheet, new ZeroDistance() as ITypedGetter<double>);
        }

        public override string ToString()
        {
            return GetType().Name;
        }

        public override int GetHashCode()
        {
            return GetType().FullName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
                return obj.GetHashCode() == GetHashCode();
        }
    }

    public class ShiftSelected : ISheetBumpType, IHaveBump, IHaveIInstrumentSelector
    {
        private readonly IInstrumentSelector _selector;
        public double BumpValue { get; private set; }
        public QuoteBumpType BumpType { get; private set; }

        public IInstrumentSelector Selector { get { return _selector; } }

        public ShiftSelected(IInstrumentSelector selector, double bumpValue, QuoteBumpType bumpType)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            BumpValue = bumpValue;
            BumpType = bumpType;
        }

        public Tuple<DataQuoteSheet, ITypedGetter<double>> Bump(DataQuoteSheet baseSheet)
        {
            var sheetList = new List<IInstrument>();
            var dist = new DictDistance();
            foreach (var item in baseSheet.Data)
            {

                if (!_selector.Select(item))
                {
                    sheetList.Add(item);
                }
                else
                {
                    var itemAsNewValue = item as IDefineNewValue;
                    if (itemAsNewValue == null)
                    {
                        continue; // TODO THROW !!!
                    }

                    var itemCpy = item.DeepCopy();

                    Add<MidQuote>(dist, itemCpy as IDefineNewValue);
                    Add<BidQuote>(dist, itemCpy as IDefineNewValue);
                    Add<AskQuote>(dist, itemCpy as IDefineNewValue);

                    sheetList.Add(itemCpy);

                }
            }

            var output = new DataQuoteSheet(baseSheet.SpotDate, sheetList);
            return Tuple.Create(output, dist as ITypedGetter<double>);
        }

        private void Add<T>(DictDistance dict, IDefineNewValue v) where T : IDataQuote
        {
            try
            {
                double baseValue = v.CurrentValue<T>();
                double effectiveBump = BumpType == QuoteBumpType.Absolute ? baseValue + BumpValue : baseValue * (1d + BumpValue);
                v.ResetValue<T>(effectiveBump);
                dict.AddOrUpdate(typeof(T), effectiveBump - baseValue);
            }
            catch (Exception)
            {
                return;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} : Selector={1} ; BumpValue={2} ; BumpType={3}", GetType().Name, _selector, BumpValue, BumpType);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            const int p2 = 23;

            unchecked
            {
                hash = hash * p2 + _selector.GetHashCode();
                hash = hash * p2 + BumpValue.GetHashCode();
                hash = hash * p2 + BumpType.GetHashCode();
            }

            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
                return obj.GetHashCode() == GetHashCode();
        }

    }

    public class SingleQuoteShift : ISheetBumpType, IHaveBump
    {
        private readonly ShiftSelected _selector;
        public double BumpValue { get { return _selector.BumpValue; } }
        public QuoteBumpType BumpType { get { return _selector.BumpType; } }


        public SingleQuoteShift(double bump, QuoteBumpType bumpType)
        {
            _selector = new ShiftSelected(new AllSelector<IInstrument>(), bump, bumpType);
        }

        public Tuple<DataQuoteSheet, ITypedGetter<double>> Bump(DataQuoteSheet baseSheet)
        {
            return _selector.Bump(baseSheet);
        }

        public override string ToString()
        {
            return string.Format("{0} : Selector={1} ; BumpValue={2} ; BumpType={3}", GetType().Name, _selector, BumpValue, BumpType);
        }

    }

    #endregion

    public interface IFinDiffMethod 
    {
        double Compute(TrsPricingRequest baseRequest
            , Dictionary<Symbol, ISheetBumpType> lower
            , Dictionary<Symbol, double> lowerDist
            , TrsPricingRequest lowerReq
            , Dictionary<Symbol, ISheetBumpType> upper
            , Dictionary<Symbol, double> upperDist
            , TrsPricingRequest upperReq
            );

        double ComputeDerivativeDelta(TrsPricingRequest baseRequest
           , Dictionary<Symbol, ISheetBumpType> lower
           , Dictionary<Symbol, double> lowerDist
           , TrsPricingRequest lowerReq
           , Dictionary<Symbol, ISheetBumpType> upper
           , Dictionary<Symbol, double> upperDist
           , TrsPricingRequest upperReq
           );
    }

    public class Derivative: IFinDiffMethod 
    {
        private const string XllName = "Derivative";
        private readonly double _norm;

        public Derivative()
        {
            _norm = 1d;
        }

        [WorksheetFunction(XllName + ".New")]
        public Derivative(double norm)
        {
            _norm = norm;
        }

        public double Compute(TrsPricingRequest baseRequest
            , Dictionary<Symbol, ISheetBumpType> lower
            , Dictionary<Symbol, double> lowerDist
            , TrsPricingRequest lowerReq
            , Dictionary<Symbol, ISheetBumpType> upper
            , Dictionary<Symbol, double> upperDist
            , TrsPricingRequest upperReq
            )
        {
            TrsPricingRequest right = lowerReq ?? baseRequest;
            TrsPricingRequest left = upperReq ?? baseRequest;
            double rightDist = lowerReq == null ? 0d : lowerDist.First().Value;
            double leftDist = upperReq == null ? 0d : upperDist.First().Value;

            return (left.DirtyPrice - right.DirtyPrice) / (leftDist - rightDist) * _norm;
        }

        public double ComputeDerivativeDelta(TrsPricingRequest baseRequest, Dictionary<Symbol, ISheetBumpType> lower, Dictionary<Symbol, double> lowerDist, TrsPricingRequest lowerReq, Dictionary<Symbol, ISheetBumpType> upper, Dictionary<Symbol, double> upperDist, TrsPricingRequest upperReq)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            return typeof(Derivative).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return obj.GetHashCode() == GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("Calc Method:{0}/{1}", typeof(Derivative).Name, _norm);
        }


    }

    public class DerivativeBasket : IFinDiffMethod
    {
        private const string XllName = "DerivativeBasket";
        private readonly double _norm;
        private readonly SecurityBasket _basket;
       
        [WorksheetFunction(XllName + ".New")]
        public DerivativeBasket(SecurityBasket basket, double norm)
        {
            _norm = norm;
            _basket = Require.ArgumentNotNull(basket, "basket");
        }

        public double Compute(TrsPricingRequest baseRequest
                    , Dictionary<Symbol, ISheetBumpType> lower
                    , Dictionary<Symbol, double> lowerDist
                    , TrsPricingRequest lowerReq
                    , Dictionary<Symbol, ISheetBumpType> upper
                    , Dictionary<Symbol, double> upperDist
                    , TrsPricingRequest upperReq
                    )
        {
            TrsPricingRequest right = lowerReq ?? baseRequest;
            TrsPricingRequest left = upperReq ?? baseRequest;
            double rightDist = 0d;
            double leftDist = 0d;

            if (lower.First().Value != null)
            {
                foreach (var item in _basket)
                {
                    rightDist += item.Weight * lowerDist[item.Underlying];
                }
            }

            if (upper.First().Value != null)
            {
                foreach (var item in _basket)
                {
                    leftDist += item.Weight * upperDist[item.Underlying];
                }
            }

            return (left.DirtyPrice - right.DirtyPrice) / (leftDist-rightDist) * _norm;
        }


        public double ComputeDerivativeDelta(TrsPricingRequest baseRequest
                    , Dictionary<Symbol, ISheetBumpType> lower
                    , Dictionary<Symbol, double> lowerDist
                    , TrsPricingRequest lowerReq
                    , Dictionary<Symbol, ISheetBumpType> upper
                    , Dictionary<Symbol, double> upperDist
                    , TrsPricingRequest upperReq
                    )
        {
            TrsPricingRequest right = lowerReq ?? baseRequest;
            TrsPricingRequest left = upperReq ?? baseRequest;
            double rigthOriginal = 0d;
            double leftOrigina = 0d;

            if (lower.First().Value != null)
            {
                foreach (var item in _basket)
                {
                    rigthOriginal += item.Weight * lowerDist[item.Underlying]/ ((AldrinAnalytics.Calibration.ShiftSelected)lower[item.Underlying]).BumpValue;
                }
            }

            if (upper.First().Value != null)
            {
                foreach (var item in _basket)
                {
                    leftOrigina += item.Weight * upperDist[item.Underlying]/ ((AldrinAnalytics.Calibration.ShiftSelected)upper[item.Underlying]).BumpValue;
                }
            }

            if (lower.First().Value != null)
            {
                return left.DirtyPrice/ rigthOriginal;
            }
            else if(upper.First().Value != null)
            {
                return right.DirtyPrice / leftOrigina;
            }
            else
            {
                return 0;
            }

        }

        public override int GetHashCode()
        {
            return typeof(DerivativeBasket).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return obj.GetHashCode() == GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("Calc Method:{0}/{1}", typeof(DerivativeBasket).Name, _norm);
        }
    }

    public class Absolute : IFinDiffMethod
    {
        private const string XllName = "Absolute";
        private readonly double _norm;

        public Absolute()
        {
            _norm = 1d;
        }

        [WorksheetFunction(XllName + ".New")]
        public Absolute(double norm)
        {
            _norm = norm;
        }

        public double Compute(TrsPricingRequest baseRequest
                            , Dictionary<Symbol, ISheetBumpType> lower
                            , Dictionary<Symbol, double> lowerDist
                            , TrsPricingRequest lowerReq
                            , Dictionary<Symbol, ISheetBumpType> upper
                            , Dictionary<Symbol, double> upperDist
                            , TrsPricingRequest upperReq
                            )
        {
            TrsPricingRequest right = lowerReq ?? baseRequest;
            TrsPricingRequest left = upperReq ?? baseRequest;
            double rightDist = lowerReq == null ? 0d : lowerDist.First().Value;
            double leftDist = upperReq == null ? 0d : upperDist.First().Value;

            double rightdelta = (left.DirtyPrice - baseRequest.DirtyPrice) * _norm;
            double leftdelta = (right.DirtyPrice - baseRequest.DirtyPrice) * _norm;

            //return (right.DirtyPrice - left.DirtyPrice)  * _norm;
            return (rightdelta+leftdelta);
        }

        double IFinDiffMethod.ComputeDerivativeDelta(TrsPricingRequest baseRequest, Dictionary<Symbol, ISheetBumpType> lower, Dictionary<Symbol, double> lowerDist, TrsPricingRequest lowerReq, Dictionary<Symbol, ISheetBumpType> upper, Dictionary<Symbol, double> upperDist, TrsPricingRequest upperReq)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            return typeof(Absolute).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return obj.GetHashCode() == GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("Calc Method:{0}/{1}", typeof(Absolute).Name, _norm);
        }


    }


    public class RelativeChange : IFinDiffMethod
    {
        private const string XllName = "RelativeChange";
        private readonly double _norm;

        public RelativeChange()
        {
            _norm = 1d;
        }

        [WorksheetFunction(XllName + ".New")]
        public RelativeChange(double norm)
        {
            _norm = norm;
        }

        public double Compute(TrsPricingRequest baseRequest
                     , Dictionary<Symbol, ISheetBumpType> lower
                     , Dictionary<Symbol, double> lowerDist
                     , TrsPricingRequest lowerReq
                     , Dictionary<Symbol, ISheetBumpType> upper
                     , Dictionary<Symbol, double> upperDist
                     , TrsPricingRequest upperReq
                     )
        {
            TrsPricingRequest right = lowerReq ?? baseRequest;
            TrsPricingRequest left = upperReq ?? baseRequest;
            double rightDist = lowerReq == null ? 0d : lowerDist.First().Value;
            double leftDist = upperReq == null ? 0d : upperDist.First().Value;

            return (right.DirtyPrice - left.DirtyPrice) / left.DirtyPrice * _norm;
        }

        public double ComputeDerivativeDelta(TrsPricingRequest baseRequest, Dictionary<Symbol, ISheetBumpType> lower, Dictionary<Symbol, double> lowerDist, TrsPricingRequest lowerReq, Dictionary<Symbol, ISheetBumpType> upper, Dictionary<Symbol, double> upperDist, TrsPricingRequest upperReq)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            return typeof(RelativeChange).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return obj.GetHashCode() == GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("Calc Method:{0}/{1}", typeof(RelativeChange).Name, _norm);
        }


    }

    public interface IFiniteDifferenceDelta
    {
        Dictionary<Symbol, ISheetBumpType> Lower { get; }
        Dictionary<Symbol, ISheetBumpType> Upper { get; }
        IFinDiffMethod DeltaMethod { get; }
    }

    public class FiniteDifferenceDelta : IFiniteDifferenceDelta 
    {
        public Dictionary<Symbol, ISheetBumpType> Lower { get; private set; }
        public Dictionary<Symbol, ISheetBumpType> Upper { get; private set; }
        public IFinDiffMethod DeltaMethod { get; private set; }

        public FiniteDifferenceDelta(Dictionary<Symbol, ISheetBumpType> lower
            , Dictionary<Symbol, ISheetBumpType> upper, IFinDiffMethod deltaMethod)
        {
            if (lower == null && upper == null)
            {
                throw new ArgumentException("At least one of the input bump should be not null but both are null !");
            }

            // TODO CHECK EGALITE DES CLEFS !

            Lower = lower;
            Upper = upper;

            DeltaMethod = Require.ArgumentNotNull(deltaMethod, "deltaMethod");
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return obj.GetHashCode() == GetHashCode();
            }
        }

        public override int GetHashCode()
        {
            int hash = 17;
            const int p2 = 23;

            unchecked
            {
                if (Lower != null)
                {
                    foreach (var item in Lower)
                    {
                        hash = hash * p2 + item.Key.GetHashCode();
                        if (item.Value!=null)
                            hash = hash * p2 + item.Value.GetHashCode();
                    }
                    
                }

                if (Upper != null)
                {
                    foreach (var item in Upper)
                    {
                        hash = hash * p2 + item.Key.GetHashCode();
                        if (item.Value != null)
                            hash = hash * p2 + item.Value.GetHashCode();
                    }
                }

                hash = hash * p2 + DeltaMethod.GetHashCode();
            }

            return hash;
        }

        public override string ToString()
        {
            // BEWARE : ONLY FIRST VALUE DISPLAYED
            return string.Format("{0} / {1} / {2}", Lower.First().Value, Upper.First().Value, DeltaMethod);
        }
    }

    public interface IBumpSheetTypeSet 
    {
        List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider);
    }

    public abstract class BumpSheetBase : IBumpSheetTypeSet
    {
        public double PositiveBump { get; private set; }
        public double NegativeBump { get; private set; }
        public QuoteBumpType BumpType { get; private set; }
        public IFinDiffMethod DeltaMethod { get; private set; }

        public BumpSheetBase(double positiveBump, double negativeBump
           , QuoteBumpType bumpType, IFinDiffMethod deltaMethod)
        {
            PositiveBump = Require.ArgumentRange(ValueRange.GreaterOrEqual(0d), positiveBump, "positiveBump"); ;
            NegativeBump = Require.ArgumentRange(ValueRange.LessOrEqual(0d), negativeBump, "negativeBump");
            BumpType = bumpType;
            DeltaMethod = Require.ArgumentNotNull(deltaMethod, "deltaMethod");
        }

        public abstract List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider);

        //public List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider)
        //{
        //    //Require.Argument(symbols.Count == 1, nameof(symbols), Error.Msg("The class {0} support only single ticker input but the provided set of symbols have a count equal to {1}", GetType().Name, symbols.Count));
        //    var output = new List<Dictionary<Symbol, IFiniteDifferenceDelta>>();
        //    var baseSheet = sheetProvider.Get(symbol);
        //    return InternalGetBumps(baseSheet);            
        //}
    }

    public class FullTenorBump<T> : BumpSheetBase where T : IInstrument
    {
        public FullTenorBump(double positiveBump, double negativeBump
            , QuoteBumpType bumpType, IFinDiffMethod deltaMethod)
            : base(positiveBump, negativeBump, bumpType, deltaMethod)
        { }

        public override List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider)
        {
            var output = new List<IFiniteDifferenceDelta>();
            var baseSheet = sheetProvider.Get(symbol);
            foreach (var item in baseSheet.Data)
            {
                if (!(item is T))
                    continue;

                var itemAsTenor = item as IHaveTenor;
                if (itemAsTenor == null)
                    continue;

                var tenorSelector = new TenorSelector<T>(itemAsTenor.Tenor);
                var shiftNeg = NegativeBump < 0d ? new ShiftSelected(tenorSelector, NegativeBump, BumpType) : null;
                var shiftPos = PositiveBump > 0d ? new ShiftSelected(tenorSelector, PositiveBump, BumpType) : null;
                
                
                output.Add(new FiniteDifferenceDelta(new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftNeg } }
                    , new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftPos } }
                    , DeltaMethod));
            }

            return output;
        }
    }

    public class FullPillarAndUnderlyingBump<T> : BumpSheetBase where T : IInstrument
    {
        public FullPillarAndUnderlyingBump(double positiveBump, double negativeBump
            , QuoteBumpType bumpType, IFinDiffMethod deltaMethod)
            : base(positiveBump, negativeBump, bumpType, deltaMethod)
        { }

        public override List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider)
        {
            var output = new List<IFiniteDifferenceDelta>();
            var baseSheet = sheetProvider.Get(symbol);
            foreach (var item in baseSheet.Data)
            {
                if (!(item is T))
                    continue;

                var itemAsPillar = item as IHavePillar;
                if (itemAsPillar == null)
                    continue;

                var itemAsU = item as IHaveUnderlying;
                if (itemAsU == null)
                    continue;

                var tenorSelector = new PillarAndUnderlyingSelector<T>(itemAsPillar.Pillar, itemAsU.Underlying);
                var shiftNeg = NegativeBump < 0d ? new ShiftSelected(tenorSelector, NegativeBump, BumpType) : null;
                var shiftPos = PositiveBump > 0d ? new ShiftSelected(tenorSelector, PositiveBump, BumpType) : null;
                output.Add(new FiniteDifferenceDelta(new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftNeg } }
                    , new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftPos } }
                    , DeltaMethod));
            }

            return output;
        }

    }

    public class FullPillarBump<T> : BumpSheetBase where T : IInstrument
    {
        public FullPillarBump(double positiveBump, double negativeBump
            , QuoteBumpType bumpType, IFinDiffMethod deltaMethod)
            : base(positiveBump, negativeBump, bumpType, deltaMethod)
        { }

        public override List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider)
        {
            var output = new List<IFiniteDifferenceDelta>();
            var baseSheet = sheetProvider.Get(symbol);
            foreach (var item in baseSheet.Data)
            {
                if (!(item is T))
                    continue;

                var itemAsPillar = item as IHavePillar;
                if (itemAsPillar == null)
                    continue;

                var tenorSelector = new PillarSelector<T>(itemAsPillar.Pillar);
                var shiftNeg = NegativeBump < 0d ? new ShiftSelected(tenorSelector, NegativeBump, BumpType) : null;
                var shiftPos = PositiveBump > 0d ? new ShiftSelected(tenorSelector, PositiveBump, BumpType) : null;
                output.Add(new FiniteDifferenceDelta(new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftNeg } }
                    , new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftPos } }
                    , DeltaMethod));
            }

            return output;
        }

    }

    public class SinglePillarBump<T> : BumpSheetBase where T : IInstrument
    {
        private readonly DateTime _pillar;
        private readonly Ticker _underlying;
        public SinglePillarBump(Ticker ticker, DateTime pillar
            , double positiveBump, double negativeBump
            , QuoteBumpType bumpType, IFinDiffMethod deltaMethod)
            : base(positiveBump, negativeBump, bumpType, deltaMethod)
        {
            _pillar = pillar;
            _underlying = Require.ArgumentNotNull(ticker, "ticker");
        }

        public override List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider)
        {
            var output = new List<IFiniteDifferenceDelta>();
            var baseSheet = sheetProvider.Get(symbol);
            var tenorSelector = new PillarAndUnderlyingSelector<T>(_pillar, _underlying);
            var shiftNeg = NegativeBump < 0d ? new ShiftSelected(tenorSelector, NegativeBump, BumpType) : null;
            var shiftPos = PositiveBump > 0d ? new ShiftSelected(tenorSelector, PositiveBump, BumpType) : null;
            output.Add(new FiniteDifferenceDelta(new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftNeg } }
                , new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftPos } }
                , DeltaMethod));
            return output;
        }

    }

    public class FlatBump<T> : BumpSheetBase where T:IInstrument
    {
        public FlatBump(double positiveBump, double negativeBump
            , QuoteBumpType bumpType, IFinDiffMethod deltaMethod)
            : base(positiveBump, negativeBump, bumpType, deltaMethod)
        { }

        public override List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider)
        {
            var output = new List<IFiniteDifferenceDelta>();
            var baseSheet = sheetProvider.Get(symbol); 
            var all = new AllSelector<T>(); ;
            //var shiftNeg = new ShiftSelected(all, NegativeBump, BumpType);
            //var shiftPos = new ShiftSelected(all, PositiveBump, BumpType);
            var shiftNeg = NegativeBump < 0d ? new ShiftSelected(all, NegativeBump, BumpType) : null;
            var shiftPos = PositiveBump > 0d ? new ShiftSelected(all, PositiveBump, BumpType) : null;

            output.Add(new FiniteDifferenceDelta(new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftNeg } }
                , new Dictionary<Symbol, ISheetBumpType>() { { symbol, shiftPos } }
                , DeltaMethod));
            return output;
        }
    }

    public class BasketBump : BumpSheetBase 
    {
        public BasketBump(double positiveBump, double negativeBump
            , QuoteBumpType bumpType, IFinDiffMethod deltaMethod)
            : base(positiveBump, negativeBump, bumpType, deltaMethod)
        { }

        public override List<IFiniteDifferenceDelta> GetBumps(Symbol symbol, ISheetGetter sheetProvider)
        {
            var basket = Require.ArgumentIsInstanceOf<SecurityBasket>(symbol, "symbol");

            var output = new List<IFiniteDifferenceDelta>();
            var all = new AllSelector<SingleNameSecurity>();

            var shiftNeg = NegativeBump < 0d ? new ShiftSelected(all, NegativeBump, BumpType) : null;
            var lower = basket.Content.ToDictionary(x => (Symbol)x, x => (ISheetBumpType)shiftNeg);
            var shiftPos = PositiveBump > 0d ? new ShiftSelected(all, PositiveBump, BumpType) : null;
            var upper = basket.Content.ToDictionary(x => (Symbol)x, x => (ISheetBumpType)shiftPos);

            output.Add(new FiniteDifferenceDelta(lower, upper, DeltaMethod));

            return output;
        }
    }

}
