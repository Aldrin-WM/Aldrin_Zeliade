using System;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Mrc;
using System.Runtime.CompilerServices;
using System.Dynamic;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Instruments
{
    public interface IHaveResetFeature
    {
        ResetType ResetPolicy
        {
            get;
        }
    }

    public enum ResetType
    {
        Notional, Price
    }

    public class AssetLegReset : AssetLegBase, IHaveResetFeature, IPerformanceLeg
    {
        private const string XllName = "AssetLegReset";
        public double Threshold { get; private set; }
        public ParametricBusinessSchedule ResetShedule { get; private set; }
        public double DivRatio { get; private set; }
        public ResetType ResetPolicy { get; private set; }

        
        private AssetLegReset(ParametricBusinessSchedule leg,
            ParametricBusinessSchedule resetShedule,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double? notional,
            double? quotity,
            double threshold,
            double divRatio,
            ResetType resetPolicy,
            string id)
            : base(leg, payerParty, receiverParty, currency, underlying, quotity, notional, id)
        {
            DivRatio = divRatio;
            Threshold = threshold;
            ResetShedule = resetShedule;
            ResetPolicy = resetPolicy;

            var basket = underlying as SecurityBasket;
            if (basket != null)
            {
                foreach (var item in basket)
                {
                    var stock2under = new CurrencyPair(currency, item.Underlying.Currency.Code);
                    AddCurrencyPair(stock2under);
                    //ugly fix
                    var fxinverse = new CurrencyPair(item.Underlying.Currency.Code, currency);
                    AddCurrencyPair(fxinverse);

                    var div2leg = new CurrencyPair(currency, item.Underlying.DividendCurrency.Code);
                    AddCurrencyPair(div2leg);
                    //ugly fix
                    fxinverse = new CurrencyPair(item.Underlying.DividendCurrency.Code, currency);
                    AddCurrencyPair(fxinverse);

                }
                var under2leg = new CurrencyPair(basket.Currency.Code, currency);
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

        [WorksheetFunction(XllName + ".NewInPrice")]
        public static AssetLegReset AssetLegPriceReset(ParametricBusinessSchedule leg,
            ParametricBusinessSchedule resetShedule,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double quotity,
            double threshold,
            double divRatio,
            string id)
        {
            ResetType resetPolicy = ResetType.Price;
            double? notional = new double?(); // not used for this type of reset product

            return new AssetLegReset(leg, resetShedule, payerParty, receiverParty, currency, underlying, notional, quotity, threshold, divRatio, resetPolicy, id);
        }

        [WorksheetFunction(XllName + ".NewInNotional")]
        public static AssetLegReset AssetLegNotionalReset(ParametricBusinessSchedule leg,
            ParametricBusinessSchedule resetShedule,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double quotity,
            double threshold,
            double divRatio,
            string id)
        {
            ResetType resetPolicy = ResetType.Notional;
            double? notional = new double?(); // will be reset by the product

            return new AssetLegReset(leg, resetShedule, payerParty, receiverParty, currency, underlying, notional, quotity, threshold, divRatio, resetPolicy, id);
        }

        public override int GetHashCode()
        {
            // A FAIRE ?
            return Guid.NewGuid().GetHashCode();
        }

        public override IProduct ToProduct(DateTime asof, bool forceMid, string pricingRef)
        {
            if (ResetPolicy == ResetType.Notional)
            {
                var p = new AssetLegResetProduct(this);
                p.ForceMid = forceMid;
                p.Reference = pricingRef;
                return p;
            }
            else
            {
                var p = new AssetLegPriceResetProduct(this);
                p.ForceMid = forceMid;
                p.Reference = pricingRef;
                return p;
            }
        }
    }

    public class AssetLegResetFloatRate : AssetLegBase, IRateLeg, IHaveResetFeature
    {
        private const string XllName = "AssetLegResetFloatRate";
        public double Threshold { get; private set; }
        public ParametricBusinessSchedule ResetShedule { get; private set; }
        public IDayCountFraction DayCountConvention { get; private set; }
        public RateReference FixingReference { get; private set; }
        public double Spread { get; private set; }
        public ResetType ResetPolicy { get; private set; }


        private AssetLegResetFloatRate(ParametricBusinessSchedule leg,
            ParametricBusinessSchedule resetShedule,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double? notional,
            double? quotity,
            double threshold,
            RateReference fixingReference,
            double spread,
            IDayCountFraction dcf,
            ResetType resetPolicy,
            string id)
            : base(leg, payerParty, receiverParty, currency, underlying, quotity, notional, id)
        {
            Threshold = threshold;
            ResetShedule = resetShedule ?? throw new ArgumentNullException(nameof(resetShedule)); ;
            DayCountConvention = dcf ?? throw new ArgumentNullException(nameof(dcf));
            FixingReference = fixingReference ?? throw new ArgumentNullException(nameof(fixingReference));
            Spread = spread;
            ResetPolicy = resetPolicy;

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


        [WorksheetFunction(XllName + ".NewInPrice")]
        public static AssetLegResetFloatRate AssetLegPriceResetFloatRate(ParametricBusinessSchedule leg,
            ParametricBusinessSchedule resetShedule,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double quotity,
            double threshold,
            RateReference fixingReference,
            double spread,
            IDayCountFraction dcf,
            string id)
        {
            ResetType resetPolicy = ResetType.Price;
            double? notional = new double?(); // not used for this type of reset product
            return new AssetLegResetFloatRate(leg, resetShedule, payerParty, receiverParty, currency, underlying, notional, quotity, threshold, fixingReference, spread, dcf, resetPolicy, id);
        }

        [WorksheetFunction(XllName + ".NewInNotional")]
        public static AssetLegResetFloatRate AssetLegNotionalResetFloatRate(ParametricBusinessSchedule leg,
            ParametricBusinessSchedule resetShedule,
            string payerParty,
            string receiverParty,
            string currency,
            Ticker underlying,
            double quotity,
            double threshold,
            RateReference fixingReference,
            double spread,
            IDayCountFraction dcf,
            string id)
        {
            ResetType resetPolicy = ResetType.Notional;
            double? notional = new double?(); // will be reset by the product
            return new AssetLegResetFloatRate(leg, resetShedule, payerParty, receiverParty, currency, underlying, notional, quotity, threshold, fixingReference, spread, dcf, resetPolicy, id);
        }

        public override int GetHashCode()
        {
            // A FAIRE ?
            return Guid.NewGuid().GetHashCode();
        }

        public override IProduct ToProduct(DateTime asof, bool forceMid, string pricingRef)
        {
            if (ResetPolicy == ResetType.Notional)
            {
                var p = new AssetLegResetFloatRateProduct(this);
                p.ForceMid = forceMid;
                p.Reference = pricingRef;
                return p;
            }
            else
            {
                var p = new AssetLegPriceResetFloatRateProduct(this);
                p.ForceMid = forceMid;
                p.Reference = pricingRef;
                return p;
            }
        }
    }


}
