using System;
using System.Linq;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Mrc;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Zeliade.Common;
using Zeliade.Finance.Common.Pricer.MonteCarlo;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Instruments
{
    public interface IAssetProduct
    { 
        string Id { get; }
        IProduct ToProduct(DateTime asof, bool forceMid, string pricingRef);
        ReadOnlyCollection<IAssetLeg> Legs { get; }
    }

    public interface IAssetLeg : IAssetProduct, IHaveForexUnderlyer
    {
        [WorksheetFunction]
        string PayerParty { get; }
        [WorksheetFunction]
        string ReceiverParty { get; }
        [WorksheetFunction]
        Currency Currency { get; }
        [WorksheetFunction]
        IPeriod Tenor { get; }
        [WorksheetFunction]
        Ticker Underlying
        {
            get;
        }
    }

    public interface IRateLeg
    {
        RateReference FixingReference
        {
            get;
        }
    }

    public interface IPerformanceLeg
    { }

    public interface IHaveForexUnderlyer
    {
        ReadOnlyCollection<CurrencyPair> FxUnderlyings { get; }
    }

    public abstract class AssetLegBase : ParametricBusinessSchedule, IAssetLeg
    {
        
        public string PayerParty { get; private set; }
        public string ReceiverParty { get; private set; }
        public Currency Currency { get; private set; }
        public Ticker Underlying { get; private set; }
        public string Id { get; private set; }
        public double? Quotity { get; private set; }
        public double? Notional { get; private set; }
        public IPeriod Tenor { get { return Period; } }

        public ReadOnlyCollection<IAssetLeg> Legs { get { return new List<IAssetLeg>() { this }.AsReadOnly(); } }

        private HashSet<CurrencyPair> _fxPairs;
        public ReadOnlyCollection<CurrencyPair> FxUnderlyings { get { return _fxPairs.ToList().AsReadOnly();  } }

        public AssetLegBase(ParametricBusinessSchedule leg,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double? quotity,
            double? notional,
            string id)
            : base(leg)
        {
            PayerParty = payerParty ?? throw new ArgumentNullException(nameof(payerParty));
            ReceiverParty = receiverParty ?? throw new ArgumentNullException(nameof(receiverParty));
            Underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Currency = new Currency(currency);
            Quotity = quotity;
            Notional = notional;
            _fxPairs = new HashSet<CurrencyPair>();
        }

        protected void AddCurrencyPair(CurrencyPair pair)
        {
            if (!_fxPairs.Contains(pair))
                _fxPairs.Add(pair);
        }

        public override int GetHashCode()
        {
            // A FAIRE ?
            return Guid.NewGuid().GetHashCode();
        }

        public abstract IProduct ToProduct(DateTime asof, bool forceMid, string pricingRef);
       
    }

    public class AssetLegStd : AssetLegBase, IPerformanceLeg
    {
        private const string XllName = "AssetLegStd";

        public double DivRatio { get; private set; }

        private AssetLegStd(ParametricBusinessSchedule leg,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double? quotity,
            double? notional,
            double divRatio,
            string id)
            : base(leg, payerParty, receiverParty, currency, underlying, quotity, notional, id)
        {
            DivRatio = divRatio;

            var basket = underlying as SecurityBasket;
            if (basket!=null)
            {
                foreach (var item in basket)
                {
                    var stock2under = new CurrencyPair(currency, item.Underlying.Currency.Code);
                    AddCurrencyPair(stock2under);
                    //ugly fix
                    var fxinverse = new CurrencyPair( item.Underlying.Currency.Code, currency);
                    AddCurrencyPair(fxinverse);

                    var div2leg = new CurrencyPair(currency, item.Underlying.DividendCurrency.Code);
                    AddCurrencyPair(div2leg);
                    //ugly fix
                    fxinverse = new CurrencyPair(item.Underlying.DividendCurrency.Code, currency);
                    AddCurrencyPair(fxinverse);

                }
                var under2leg = new CurrencyPair(basket.Currency.Code, currency) ;
                AddCurrencyPair(under2leg);
                //ugly fix
                var fxinv = new CurrencyPair(currency, basket.Currency.Code);
                AddCurrencyPair(fxinv);

            }
            else
            {
                var sn = Require.ArgumentIsInstanceOf<SingleNameTicker>(underlying, "underlying");
                var stock2under = new CurrencyPair(currency, sn.Currency.Code);
                AddCurrencyPair(stock2under);
                //ugly fix
                var fxinverse = new CurrencyPair(sn.Currency.Code, currency);
                AddCurrencyPair(fxinverse);


                var div2leg = new CurrencyPair(currency, sn.DividendCurrency.Code);
                AddCurrencyPair(div2leg);
                //ugly fix
                fxinverse = new CurrencyPair(sn.DividendCurrency.Code, currency);
                AddCurrencyPair(fxinverse);


            }

        }

        [WorksheetFunction(XllName+".New")]
        public static  AssetLegStd New(ParametricBusinessSchedule leg,
           string payerParty,
           string receiverParty,
           string currency,
           Ticker underlying,
           double quotity,
           double divRatio,
           string id)
        {
            return new AssetLegStd(leg, payerParty, receiverParty, currency, underlying, quotity, new double?(), divRatio, id);
        }

        [WorksheetFunction(XllName + ".NewNotional")]
        public static AssetLegStd NewNotional(ParametricBusinessSchedule leg,
           string payerParty,
           string receiverParty,
           string currency,
           Ticker underlying,
           double notional,
           double divRatio,
           string id)
        {
            return new AssetLegStd(leg, payerParty, receiverParty, currency, underlying, new double?(), notional, divRatio, id);
        }

        public override int GetHashCode()
        {
            // A FAIRE ?
            return Guid.NewGuid().GetHashCode();
        }

        public override IProduct ToProduct(DateTime asof, bool forceMid, string pricingRef)
        {
            var p = new AssetLegProduct(this); // TODO TRONQUER
            p.ForceMid = forceMid;
            p.Reference = pricingRef;
            return p;
        }
    }

    public class AssetLegFloatRate : AssetLegBase, IRateLeg
    {
        private const string XllName = "AssetLegFloatRate";

        public IDayCountFraction DayCountConvention { get; private set; }
        public RateReference FixingReference { get; private set; }
        public double Spread { get; private set; }
        public double? Threshold { get; private set; }
        public ParametricBusinessSchedule ResetShedule { get; private set; }

        private AssetLegFloatRate(ParametricBusinessSchedule leg,
           ParametricBusinessSchedule resetShedule,
           string payerParty,
           string receiverParty,
           string currency,
           Ticker underlying,
           RateReference fixingReference,
           double spread,
           IDayCountFraction dcf,
           double? quotity,
           double? threshold,
           double? notional,
           string id)
           : base(leg, payerParty, receiverParty, currency, underlying, quotity, notional, id)
        {
            DayCountConvention = dcf ?? throw new ArgumentNullException(nameof(dcf));
            FixingReference = fixingReference ?? throw new ArgumentNullException(nameof(fixingReference));
            Spread = spread;

            if (resetShedule != null) {
                ResetShedule = resetShedule;
                Threshold = threshold;
            }


            var basket = underlying as SecurityBasket;
            if (basket != null)
            {
                foreach (var item in basket)
                {
                    var stock2leg = new CurrencyPair(currency, item.Underlying.Currency.Code);
                    AddCurrencyPair(stock2leg);
                }
            }
            else
            {
                var sn = Require.ArgumentIsInstanceOf<SingleNameTicker>(underlying, "underlying");
                var stock2leg = new CurrencyPair(currency, sn.Currency.Code);
                AddCurrencyPair(stock2leg);
            }

        }

        [WorksheetFunction(XllName+".New")]
        public AssetLegFloatRate(ParametricBusinessSchedule leg,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            RateReference fixingReference,
            double spread,
            IDayCountFraction dcf,
            double quotity,
            string id)
            : this(leg, null, payerParty, receiverParty, currency, underlying, fixingReference, spread, dcf, quotity, new double?(), new double?(), id)
        {

        }

        [WorksheetFunction(XllName + ".NewNotional")]
        public static AssetLegFloatRate NewNotional(ParametricBusinessSchedule leg,
          string payerParty,
          string receiverParty,
          string currency,
          Ticker underlying,
          RateReference fixingReference,
          double spread,
          IDayCountFraction dcf,
          double notional,
          string id)
        {
            ParametricBusinessSchedule NoReset = null;
            double? Nothreshold = new double?();
            return new AssetLegFloatRate(leg,NoReset, payerParty, receiverParty, currency, underlying, fixingReference, spread, dcf, new double?(), Nothreshold, notional, id);            
        }

        [WorksheetFunction(XllName + ".NewInPrice")]
        public static AssetLegFloatRate AssetLegPriceReset(ParametricBusinessSchedule leg,
          ParametricBusinessSchedule resetShedule,
          string payerParty,
          string receiverParty,
          string currency,
          Ticker underlying,
          RateReference fixingReference,
          double spread,
          IDayCountFraction dcf,
          double quotity,
          double threshold,
          string id)
        {
           
            double? notional = new double?(); // not used for this type of reset product
            return new AssetLegFloatRate(leg,resetShedule, payerParty, receiverParty, currency, underlying, fixingReference, spread, dcf, quotity, threshold,notional, id);
        }

        public override int GetHashCode()
        {
            // A FAIRE ?
            return Guid.NewGuid().GetHashCode();
        }

        public override IProduct ToProduct(DateTime asof, bool forceMid, string pricingRef)
        {
            if (this.ResetShedule != null) 
            {
                var p = new AssetLegFloatRateProductPriceReset(this);
                p.ForceMid = forceMid;
                p.Reference = pricingRef;
                return p;
            }
            else
            {
                var p = new AssetLegFloatRateProduct(this);
                p.ForceMid = forceMid;
                p.Reference = pricingRef;
                return p;
            }
           
        }
    }

}
