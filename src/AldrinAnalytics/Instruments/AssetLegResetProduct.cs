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
    public class AssetLegResetProduct : ProductBase, IHavePricingReference
    {
        private readonly SecurityBasket _basket;
        private readonly AssetLegReset _assetLegReset;
        private double[] _lastFixing;
        private DateTime _lastFixingDate;
        private List<string> _listCCYs;
        private Dictionary<string, double> _lastFXRates;
        private double _currentQuotity;
        private double _initialQuotity;
        private Type _stockType;
        private Type _divType;
        private bool _firstFix;
        private double _notional;


        public bool ForceMid { get; set; }
        public string Reference
        {
            set
            {
                if ((value == _assetLegReset.PayerParty) && !ForceMid)
                {
                    _stockType = typeof(BidQuote);
                    _divType = typeof(BidQuote);
                }
                else if ((value == _assetLegReset.ReceiverParty) && !ForceMid)
                {
                    _stockType = typeof(AskQuote);
                    _divType = typeof(AskQuote);
                }
                else
                {
                    _stockType = typeof(MidQuote);
                    _divType = typeof(MidQuote);
                }
            }
        }

        public AssetLegResetProduct(AssetLegReset assetLegReset)
            : base(assetLegReset.Id, assetLegReset.Start, assetLegReset.End)
        {
            _assetLegReset = assetLegReset;
            _basket = assetLegReset.Underlying as SecurityBasket; // TODO CHECK TYPE
            _listCCYs = _basket.Components.Select(x => x.Underlying.Currency.Code).Distinct().ToList();
            _lastFXRates = new Dictionary<string, double>();

            AddCurrency(assetLegReset.Currency);

            ForceMid = true;

            AddParty(assetLegReset.PayerParty);
            AddParty(assetLegReset.ReceiverParty);

            AddCallBack(assetLegReset, AssetPaymentLeg, -1);
            AddCallBack(assetLegReset.AllDates.Take(assetLegReset.Count), Fixing, -1);

            AddCallBack(assetLegReset.AllDates[0], ResetInitializer, -1);
            AddCallBack(assetLegReset.ResetShedule, ResetQuotity, -1);

            _stockType = typeof(MidQuote);
            _divType = typeof(MidQuote);

            Reset();
            _firstFix = true;
        }

        private CallBackOutput Fixing(CallBackArg arg)
        {
            //var m = arg.Model as IJointModel;
            //_lastFixing = m.StockValues(_stockType);
            //_lastFixingDate = arg.CurrentDate;
            //return CallBackOutput.EmptyPaymentOutput();
            var m = arg.Model as IJointModel;
            _lastFixing = m.StockValues(_stockType);
            _lastFixingDate = arg.CurrentDate;

            foreach (var ccy in _listCCYs)
            {
                var ccyPair = Tuple.Create(ccy, _assetLegReset.Currency.Code);
                _lastFXRates[ccy] = m.FxValue(ccyPair, typeof(MidQuote));
            }




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

        protected override void InternalReset()
        {
            _currentQuotity = _initialQuotity;
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
            var output = new CallBackOutput(2);

            // Asset perf
            double payoff = 0d;
            var comps = _basket.Components;
            var stocks = m.StockValues(_stockType);
            for (int i = 0; i < comps.Count; i++)
            {
                var ccyStock = comps[i].Underlying.Currency.Code;
                var ccyPair = Tuple.Create(ccyStock, _assetLegReset.Currency.Code);
                var fxRate = m.FxValue(ccyPair, typeof(MidQuote));
                payoff += comps[i].Weight * (stocks[i] * fxRate - _lastFixing[i] * _lastFXRates[ccyStock]);
            }

            payoff *= _currentQuotity;

            output.AddPayment(_assetLegReset.PayerParty, _assetLegReset.ReceiverParty, _assetLegReset.Currency.Code, payoff, "AssetPerf", false);

            // Dividends --> deterministe
            var payoffDiv = 0d;
            var divs = m.Dividends(_lastFixingDate, _divType);
            foreach (var item in divs)
            {
                var ccyPair = Tuple.Create(item.PaymentCurrency.Code, _assetLegReset.Currency.Code);
                var fx = m.FxValue(ccyPair, typeof(MidQuote));
                double weight = 1d;
                SingleNameTicker snTicker = item.Ticker as SingleNameTicker;
                if (snTicker != null)
                    weight = _basket.GetComponent(snTicker).Weight;

                payoffDiv += _assetLegReset.DivRatio * weight * item.GrossAmount * fx;
            }
            payoffDiv *= _currentQuotity;

            output.AddPayment(_assetLegReset.PayerParty, _assetLegReset.ReceiverParty, _assetLegReset.Currency.Code, payoffDiv, "Dividends", false);

            return output;
        }



    }
}
