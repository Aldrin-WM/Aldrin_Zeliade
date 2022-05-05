using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Models;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Instruments
{
    public class AssetLegResetFloatRateProduct : ProductBase, IHavePricingReference
    {
        private readonly SecurityBasket _basket;
        private readonly AssetLegResetFloatRate _assetLegReset;
        private double[] _lastFixing;
        private DateTime _lastFixingDate;
        private double _currentBaskValue;
        private double _currentFixing;
        private double _currentQuotity;
        private double _initialQuotity;
        private double _notional;
        private Type _stockType;
        private Type _rateType;
        private bool _firstFix = true;
        public bool ForceMid { get; set; }
        public string Reference
        {
            set
            {
                if ((value == _assetLegReset.PayerParty) && !ForceMid)
                {
                    _stockType = typeof(BidQuote);
                    _rateType = typeof(AskQuote);
                }
                else if ((value == _assetLegReset.ReceiverParty) && !ForceMid)
                {
                    _stockType = typeof(AskQuote);
                    _rateType = typeof(BidQuote);
                }
                else // Un peu dangereux. Faire un flag ForceMid + erreur si Reference pas incluse dans les contreparties leg ?
                {
                    _stockType = typeof(MidQuote);
                    _rateType = typeof(MidQuote);
                }
            }
        }


        public AssetLegResetFloatRateProduct(AssetLegResetFloatRate assetLegReset)
            : base(assetLegReset.Id, assetLegReset.Start, assetLegReset.End)
        {
            _assetLegReset = assetLegReset;
            _basket = assetLegReset.Underlying as SecurityBasket; // TODO CHECK TYPE
            
            AddCurrency(assetLegReset.Currency);

            AddParty(assetLegReset.PayerParty);
            AddParty(assetLegReset.ReceiverParty);

            AddCallBack(assetLegReset, AssetPaymentLeg, -1);
            AddCallBack(assetLegReset.AllDates.Take(assetLegReset.Count), Fixing, -1);

            AddCallBack(assetLegReset.AllDates[0], ResetInitializer, -1);
            AddCallBack(assetLegReset.ResetShedule, ResetQuotity, -1);

            var refAsOis = assetLegReset.FixingReference as OisReference;
            if (refAsOis != null)
            {
                refAsOis.Tenor = assetLegReset.Period;
            }

            _stockType = typeof(MidQuote);
            _rateType = typeof(MidQuote);

            _firstFix = true;

            Reset();
        }

        private CallBackOutput Fixing(CallBackArg arg)
        {
            var m = arg.Model as IJointModel;
            _lastFixingDate = arg.CurrentDate;

            var comps = _basket.Components;
            var stocks = m.StockValues(_stockType);
            _currentBaskValue = 0d;
            for (int i = 0; i < comps.Count; i++)
            {
                var ccyStock = comps[i].Underlying.Currency.Code;
                var ccyPair = Tuple.Create(ccyStock, _assetLegReset.Currency.Code);
                _currentBaskValue += comps[i].Weight * stocks[i] * m.FxValue(ccyPair, typeof(MidQuote));
            }

            _currentFixing = _assetLegReset.FixingReference.Fixing(m, _rateType);

            return CallBackOutput.EmptyPaymentOutput();

        }

        private CallBackOutput ResetInitializer(CallBackArg arg)
        {
            if (_firstFix)
            {
                var m = arg.Model as IJointModel;

                var basketValue = 0d;
                var comps = _basket.Components;
                var stocks = m.StockValues(_stockType);
                for (int i = 0; i < comps.Count; i++)
                {
                    var ccyStock = comps[i].Underlying.Currency.Code;
                    var ccyPair = Tuple.Create(ccyStock, _assetLegReset.Currency.Code);
                    var fx = m.FxValue(ccyPair, typeof(MidQuote));
                    basketValue += comps[i].Weight * stocks[i] * fx;
                }

                if (_assetLegReset.Quotity.HasValue)
                {
                    //Basket value is calculated as t0+1, it should be t0
                    _initialQuotity = _assetLegReset.Quotity.Value;
                    _notional = _initialQuotity * basketValue;
                }
                else
                {
                    //this ugly fix
                    _initialQuotity = 1;
                    _notional = _assetLegReset.Notional.Value;
                }
                _currentQuotity = _initialQuotity;
                _firstFix = false;
            }
            return CallBackOutput.EmptyPaymentOutput();
        }

        private CallBackOutput ResetQuotity(CallBackArg arg)
        {
            var m = arg.Model as IJointModel;

            double currentBasketValue = 0d;
            var comps = _basket.Components;
            var stocks = m.StockValues(_stockType);
            for (int i = 0; i < comps.Count; i++)
            {
                var ccyStock = comps[i].Underlying.Currency.Code;
                var ccyPair = Tuple.Create(ccyStock, _assetLegReset.Currency.Code);
                var fx = m.FxValue(ccyPair, typeof(MidQuote));
                currentBasketValue += comps[i].Weight * stocks[i] * fx;
            }

            if (Math.Abs(_currentQuotity * currentBasketValue - _notional) > _assetLegReset.Threshold)
            {
                var pay = AssetPaymentLeg(arg);
                Fixing(arg);
                _currentQuotity = _notional / currentBasketValue;

            }
            return CallBackOutput.EmptyPaymentOutput();
        }

        private CallBackOutput AssetPaymentLeg(CallBackArg arg)
        {
            var m = arg.Model as IJointModel;
            var output = new CallBackOutput(1);

            double period = _assetLegReset.DayCountConvention.Count(_lastFixingDate, arg.CurrentDate);

            double duration = period * _currentBaskValue * _currentQuotity;

            double payoff = duration * (_currentFixing + _assetLegReset.Spread);
            double fltRateComponent = duration * _currentFixing;
            output.AddPayment(_assetLegReset.PayerParty, _assetLegReset.ReceiverParty, _assetLegReset.Currency.Code, payoff, "RateLeg", false);
            output.AddObservable("Duration", duration);
            output.AddObservable("FltRateComp", fltRateComponent);

            return output;
        }


    }
}
