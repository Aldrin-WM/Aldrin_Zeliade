using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
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
    public interface IGenericMarket
    {
        List<Symbol> RegisteredTicker { get; }
    }

    public interface ISheetGetter
    {
        DataQuoteSheet Get(Symbol ticker);
    }

    public interface IGenericMarket<K, T> : IGenericMarket where K : Symbol where T:class
    {
        DateTime MarketDate { get; }

        T Get(K ticker, ISheetBumpType bump, Type quoteType);

        double GetDistance(K ticker, ISheetBumpType bump, Type quoteType);

        
        ReadOnlyDictionary<Symbol, List<IFiniteDifferenceDelta>> Bumps
        {
            get;
        }
    }

    public interface IBumpSetter : IGenericMarket
    {
        IGenericMarket SetBump(Symbol underlying, IBumpSheetTypeSet bumpPolicy) ;
    }

    public interface IGenericMarketSetter<K, T> : IBumpSetter where K:Symbol where T : class
    {       
        IGenericMarket<K, T> AddSheet(K underlying, DataQuoteSheet sheet, IGenericBootstrapper<T> bootstrapper);
        //IGenericMarket<K, T> SetBump(K underlying, IBumpSheetTypeSet bumpPolicy);
    }

    public class RegisteredSymbols
    {
        private const string XllName = "GenericMarket";

        protected List<Symbol> _symbols;

        [WorksheetFunction(XllName+ ".RegisteredTickerNames")]
        public string[] RegisteredTickerNames { get { return _symbols.Select(x=>x.Name).ToArray(); } }
    }


    public class GenericMarket<K,T> : RegisteredSymbols, IGenericMarket<K,T>, ISheetGetter, IGenericMarketSetter<K, T> where K: Symbol where T:class
    {

        private readonly Dictionary<Tuple<K, Type>, T> _market;
        protected readonly Dictionary<K, DataQuoteSheet> _sheets;
        protected readonly Dictionary<K, IGenericBootstrapper<T>> _bootstrappers;
        private readonly HashSet<Tuple<K, ISheetBumpType>> _bootstrapKeys;
        private readonly Dictionary<Tuple<K, Type, ISheetBumpType>, T> _bumpMarket;
        private readonly Dictionary<Tuple<K, Type, ISheetBumpType>, double> _distMarket;
        private readonly Dictionary<Symbol, List<IFiniteDifferenceDelta>> _bumps;

        public DateTime MarketDate { get; private set; }

        public ReadOnlyDictionary<Symbol, List<IFiniteDifferenceDelta>> Bumps
        {
            get
            {
                return new ReadOnlyDictionary<Symbol, List<IFiniteDifferenceDelta>>(_bumps);
            }
        }

        public GenericMarket(DateTime marketDate)
        {
            MarketDate = marketDate;
            _market = new Dictionary<Tuple<K, Type>, T>();
            _sheets = new Dictionary<K, DataQuoteSheet>();
            _bootstrappers = new Dictionary<K, IGenericBootstrapper<T>>();
            _bumpMarket = new Dictionary<Tuple<K, Type, ISheetBumpType>, T>();
            _distMarket = new Dictionary<Tuple<K, Type, ISheetBumpType>, double>();
            _bumps = new Dictionary<Symbol, List<IFiniteDifferenceDelta>>();

            _symbols = new List<Symbol>();
            _bootstrapKeys = new HashSet<Tuple<K, ISheetBumpType>>();
        }
        
        public List<Symbol> RegisteredTicker
        {
            get { return _sheets.Keys.Cast<Symbol>().ToList(); }
        }
        
        public bool Contains(K ticker)
        {
            return _sheets.ContainsKey(ticker);
        }

        public T Get(K ticker, ISheetBumpType bump, Type quoteType)
        {
            Require.ArgumentNotNull(ticker, "ticker");
            T curve = null;

            if (bump != null)
            {
                var key = Tuple.Create(ticker, quoteType, bump);

                if (!_bumpMarket.TryGetValue(key, out curve))
                {
                    //throw new KeyNotFoundException(string.Format("The curve for {0}/{1}/{2} is not found in the market {3} !", ticker, quoteType.Name, bump, typeof(T)));
                    var keyBase = Tuple.Create(ticker, quoteType);
                    if (!_market.TryGetValue(keyBase, out curve))
                    {
                        throw new KeyNotFoundException(string.Format("The curve for {0}/{1} is not found in the {2} market !", ticker, quoteType.Name, typeof(T)));
                    }

                }
            }
            else
            {
                var key = Tuple.Create(ticker, quoteType);
                if (!_market.TryGetValue(key, out curve))
                {
                    throw new KeyNotFoundException(string.Format("The curve for {0}/{1} is not found in the {2} market !", ticker, quoteType.Name, typeof(T)));
                }

            }

            return curve;
        }

        public double GetDistance(K ticker, ISheetBumpType bump, Type quoteType)
        {
            if (!_sheets.ContainsKey(ticker))
            {
                throw new ArgumentException(string.Format("The ticker {0} is not registered in the market !", ticker.Name));
            }

            if (bump==null) // base state
            {
                return 0d;
            }

            var key = Tuple.Create(ticker, quoteType, bump);
            double dist;
            if (!_distMarket.TryGetValue(key, out dist))
            {
                throw new KeyNotFoundException(string.Format("The curve for {0}/{1}/{2} is not found in the market {3} !", ticker, quoteType.Name, bump, typeof(T)));
            }
            return dist;
        }

        public List<IFiniteDifferenceDelta> GetBumps(K ticker)
        {
            Require.ArgumentNotNull(ticker, "ticker");
            List<IFiniteDifferenceDelta> output = null;
            
            if (!_bumps.TryGetValue(ticker, out output))
            {
                //throw new KeyNotFoundException(string.Format("No for ticker {0} is not found in the {1} market !", ticker, typeof(T)));
                return new List<IFiniteDifferenceDelta>();
            }

            return output.ToList();
        }

        public IGenericMarket<K,T> AddSheet(K underlying, DataQuoteSheet sheet, IGenericBootstrapper<T> bootstrapper)
        {
            InternalAddSheet(underlying, sheet);

            if (!_sheets.ContainsKey(underlying))
            {
                _sheets.Add(underlying, sheet);
                _symbols.Add(underlying);
                _bootstrappers.Add(underlying, bootstrapper); 

                var curves = bootstrapper.Bootstrap(sheet);
                foreach (var item in curves)
                {
                    var key = Tuple.Create(underlying, item.Key);
                    _market.Add(key, item.Value);
                }
                //_bumps.Add(underlying, new HashSet<IFiniteDifferenceDelta>());
            }
            else
            {
                throw new ArgumentException(string.Format("Sheet already added in market {0} for ticker {1} !", typeof(T).Name, underlying));
            }
                    
            return this;
        }

        protected virtual void InternalAddSheet(K underlying, DataQuoteSheet sheet)
        { }

        //public IGenericMarket SetBump(Symbol symb, IBumpSheetTypeSet bumpPolicy)
        //{
        //    var key = new SymbolSet();
        //    key.Add(symb);             
        //    SetBump(symb, bumpPolicy);
        //    return this;
        //}

        public IGenericMarket SetBump(Symbol symbol, IBumpSheetTypeSet bumpPolicy) 
        {


            var bumps = bumpPolicy.GetBumps(symbol, this);
                
            List<IFiniteDifferenceDelta> l = null;
            if (!_bumps.TryGetValue(symbol, out l))
            {
                l = new List<IFiniteDifferenceDelta>();
                _bumps.Add(symbol, l);
            }

            l.AddRange(bumps);

            foreach (var item in bumps)
            {              
                foreach (var kv in item.Lower)
                {
                    var upper = item.Upper[kv.Key];
                    InternalSetBump((K)kv.Key, kv.Value, upper);
                }  
            }
                      
            return this;
        }

        private void InternalSetBump(K underlying
            , ISheetBumpType lowerBump
            , ISheetBumpType upperBump)
        {
            var bootstrapper = _bootstrappers[underlying];
            var sheet = _sheets[underlying];

            var bootKeyLower = Tuple.Create(underlying, lowerBump);

            if (lowerBump != null && !_bootstrapKeys.Contains(bootKeyLower))
            {
                var lower = lowerBump.Bump(sheet);
                var negCurves = bootstrapper.Bootstrap(lower.Item1);
                _bootstrapKeys.Add(bootKeyLower);
                foreach (var c in negCurves)
                {
                    var keyLower = Tuple.Create(underlying, c.Key, lowerBump);
                    _bumpMarket.Add(keyLower, c.Value);
                    _distMarket.Add(keyLower, lower.Item2.Get(c.Key));
                }
            }

            var bootKeyUpper = Tuple.Create(underlying, upperBump);

            if (upperBump != null && !_bootstrapKeys.Contains(bootKeyUpper))
            {
                var upper = upperBump.Bump(sheet);
                var posCurves = bootstrapper.Bootstrap(upper.Item1);
                _bootstrapKeys.Add(bootKeyUpper);
                foreach (var c in posCurves)
                {
                    var keyUpper = Tuple.Create(underlying, c.Key, upperBump);
                    _bumpMarket.Add(keyUpper, c.Value);
                    _distMarket.Add(keyUpper, upper.Item2.Get(c.Key));
                }
            }

        }

        public DataQuoteSheet Get(Symbol ticker)
        {
            var u = Require.ArgumentIsInstanceOf<K>(ticker, "ticker", Error.Msg("The input ticker is of type {0} but should be of type {1} !", ticker.GetType(), typeof(K)));
            if (!_sheets.ContainsKey(u))
            {
                throw new ArgumentException(string.Format("The ticker {0} is not registered in the market !", ticker.Name));
            }
            return _sheets[u];
        }
    }

    public class GenericMarketProxy<K, T> where K:Symbol where T : class
    {
        private readonly IGenericMarket<K, T> _baseMarket;
        private IDictionary<K, ISheetBumpType> _context;

        public IDictionary<K, ISheetBumpType> Context 
        {
            get { return _context; }
            set { _dependencies.Clear(); _context = value; }
        }

        private readonly HashSet<Tuple<K, Type>> _dependencies;

        public DateTime MarketDate { get { return _baseMarket.MarketDate; } }

        public GenericMarketProxy(IGenericMarket<K, T> baseMarket)
        {
            _baseMarket = baseMarket ?? throw new ArgumentNullException(nameof(baseMarket));
            _dependencies = new HashSet<Tuple<K, Type>>();
        }

        public Tuple<K, Type>[] Dependencies
        {
            get { return _dependencies.ToArray(); }
        }

        public T Get(K ticker, Type quoteType)
        {
            var dependencyKey = Tuple.Create(ticker, quoteType);
            if (!_dependencies.Contains(dependencyKey))
                _dependencies.Add(dependencyKey);

            ISheetBumpType ctx = null;
            if (Context!=null)
                Context.TryGetValue(ticker, out ctx);

            return _baseMarket.Get(ticker, ctx, quoteType);
        }

        public double GetDistance(K ticker, ISheetBumpType bump, Type quoteType)
        {
            return _baseMarket.GetDistance(ticker, bump, quoteType);
        }   

        public ReadOnlyDictionary<Symbol, List<IFiniteDifferenceDelta>> Bumps
        {
            get
            {
                return _baseMarket.Bumps;
            }
        }

        public void ResetDependencies()
        {
            _dependencies.Clear();
        }
    }
}
