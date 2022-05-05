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

    public interface IHavePricingReference
    {
        string Reference { set; }
    }

    public class AssetLegProduct : ProductBase, IHavePricingReference
    {
        private readonly SecurityBasket _basket;
        private readonly AssetLegStd _assetLeg;
        private double[] _lastFixing;
        private DateTime _lastFixingDate;
        private List<string> _listCCYs;
        private Dictionary<string, double> _lastFXRates;
        private Type _stockType;
        private Type _divType;
        private bool _firstFix;
        private double _quotity;

        public bool ForceMid { set; get; }

        public string Reference 
        {
            set 
            {
                if ((value == _assetLeg.PayerParty)&& !ForceMid)
                {
                    _stockType = typeof(AskQuote);
                    _divType = typeof(AskQuote);
                }
                else if ((value == _assetLeg.ReceiverParty) && !ForceMid)
                {
                    _stockType = typeof(BidQuote);
                    _divType = typeof(BidQuote);
                }
                else // Un peu dangereux. Faire un flag ForceMid + erreur si Reference pas incluse dans les contreparties leg ?
                {
                    _stockType = typeof(MidQuote);
                    _divType = typeof(MidQuote);
                }
            }
        }

        public AssetLegProduct(AssetLegStd leg)
            : base(leg.Id, leg.Start, leg.End, leg.Currency.Code)
        {
            _assetLeg = leg;
            _basket = leg.Underlying as SecurityBasket; // TODO CHECK TYPE
            _listCCYs = _basket.Components.Select(x => x.Underlying.Currency.Code).Distinct().ToList();
            _lastFXRates = new Dictionary<string, double>();

            AddCurrency(leg.Currency);

            ForceMid = true;

            AddParty(leg.PayerParty);
            AddParty(leg.ReceiverParty);

            AddCallBack(leg, AssetPaymentLeg1, -1);
            AddCallBack(leg.AllDates.Take(leg.Count), Fixing, -1);

            _stockType = typeof(MidQuote);
            _divType = typeof(MidQuote);

            Reset();
            _firstFix = true;

        }

        private CallBackOutput Fixing(CallBackArg arg)
        {
            var m = arg.Model as IJointModel;
            _lastFixing = m.StockValues(_stockType);
            _lastFixingDate = arg.CurrentDate;

            foreach (var ccy in _listCCYs)
            {
                var ccyPair = Tuple.Create(ccy, _assetLeg.Currency.Code);
                _lastFXRates[ccy] = m.FxValue(ccyPair, typeof(MidQuote));
            }          

            if (_firstFix)
            {
                var comps = _basket.Components;
                var currentBaskValue = 0d;
                for (int i = 0; i < comps.Count; i++)
                {
                    var ccyStock = comps[i].Underlying.Currency.Code;
                    var ccyPair = Tuple.Create(ccyStock, _assetLeg.Currency.Code);
                    currentBaskValue += comps[i].Weight * _lastFixing[i] * m.FxValue(ccyPair, typeof(MidQuote));
                }
                if (_assetLeg.Quotity.HasValue)
                {
                    _quotity = _assetLeg.Quotity.Value;
                }
                else
                {
                    _quotity = _assetLeg.Notional.Value / currentBaskValue;
                }
                _firstFix = false;
            }


            return CallBackOutput.EmptyPaymentOutput();
        }

        private CallBackOutput AssetPaymentLeg1(CallBackArg arg)
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
                var ccyPair = Tuple.Create(ccyStock, _assetLeg.Currency.Code);
                var fxRate = m.FxValue(ccyPair, typeof(MidQuote));
                payoff += comps[i].Weight * (stocks[i] * fxRate - _lastFixing[i] * _lastFXRates[ccyStock]) ;
            }

            payoff *= _quotity;

            output.AddPayment(_assetLeg.PayerParty, _assetLeg.ReceiverParty, _assetLeg.Currency.Code, payoff, "AssetPerf", false);           

            // Dividends --> deterministe
            var payoffDiv = 0d;
            var divs = m.Dividends(_lastFixingDate, _divType);
            foreach (var item in divs)
            {
                var ccyPair = Tuple.Create(item.PaymentCurrency.Code, _assetLeg.Currency.Code);
                var fx = m.FxValue(ccyPair, typeof(MidQuote));
                double weight = 1d;
                SingleNameTicker snTicker = item.Ticker as SingleNameTicker;
                if (snTicker != null)
                    weight = _basket.GetComponent(snTicker).Weight;

                payoffDiv += _assetLeg.DivRatio * weight * item.GrossAmount * fx;
            }
            payoffDiv *= _quotity;

            output.AddPayment(_assetLeg.PayerParty, _assetLeg.ReceiverParty, _assetLeg.Currency.Code, payoffDiv, "Dividends", false);

            return output;
        }

    }
}
