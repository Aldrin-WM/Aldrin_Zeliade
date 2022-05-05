using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Models;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Instruments
{
    public class AssetLegFloatRateProduct : ProductBase, IHavePricingReference
    {
        private readonly SecurityBasket _basket;
        private readonly AssetLegFloatRate _assetLeg;
        private DateTime _lastFixingDate;
        private double _currentBaskValue;
        private double _currentFixing;
        private Type _stockType;
        private Type _rateType;
        private double _quotity;
        private bool _firstFix;
        public bool ForceMid { get; set; }
        public string Reference
        {
            set
            {
                if ((value == _assetLeg.PayerParty)&&!ForceMid)
                {
                    _stockType = typeof(BidQuote);
                    _rateType = typeof(AskQuote);
                }
                else if ((value == _assetLeg.ReceiverParty)&& !ForceMid)
                {
                    _stockType = typeof(AskQuote);
                    _rateType = typeof(BidQuote);
                }
                else
                {
                    _stockType = typeof(MidQuote);
                    _rateType = typeof(MidQuote);
                }
            }
        }

        public AssetLegFloatRateProduct(AssetLegFloatRate leg)
            : base(leg.Id, leg.Start, leg.End, leg.Currency.Code)
        {
            _assetLeg = leg;
            _basket = leg.Underlying as SecurityBasket; // TODO CHECK TYPE

            AddCurrency(leg.Currency);

            AddParty(leg.PayerParty);
            AddParty(leg.ReceiverParty);

            AddCallBack(leg, AssetPaymentLeg1, -1);
            //To Avoid Issue with first fixing
            AddCallBack(leg.AllDates[0], Fixing, -1);
            AddCallBack(leg, Fixing, -1);

            var refAsOis = _assetLeg.FixingReference as OisReference;
            if (refAsOis!=null)
            {
                refAsOis.Tenor = leg.Tenor;
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
                var ccyPair = Tuple.Create(ccyStock, _assetLeg.Currency.Code);
                _currentBaskValue += comps[i].Weight * stocks[i] * m.FxValue(ccyPair, typeof(MidQuote));
            }

            _currentFixing = _assetLeg.FixingReference.Fixing(m, _rateType);

            if (_firstFix)
            {
                if (_assetLeg.Quotity.HasValue)
                {
                    _quotity = _assetLeg.Quotity.Value;
                }
                else
                {
                    _quotity = _assetLeg.Notional.Value / _currentBaskValue;
                }
                _firstFix = false;
            }


            return CallBackOutput.EmptyPaymentOutput();
        }

        private CallBackOutput AssetPaymentLeg1(CallBackArg arg)
        {
            var m = arg.Model as IJointModel;
            var output = new CallBackOutput(1);

            double period = _assetLeg.DayCountConvention.Count(_lastFixingDate, arg.CurrentDate);

            double duration = period * _currentBaskValue * _quotity;

            double payoff = duration * (_currentFixing + _assetLeg.Spread);
            double fltRateComponent = duration * _currentFixing;
            output.AddPayment(_assetLeg.PayerParty, _assetLeg.ReceiverParty, _assetLeg.Currency.Code, payoff, "RateLeg", false);
            output.AddObservable("Duration", duration);
            output.AddObservable("FltRateComp", fltRateComponent);

            return output;
        }

    }
}
