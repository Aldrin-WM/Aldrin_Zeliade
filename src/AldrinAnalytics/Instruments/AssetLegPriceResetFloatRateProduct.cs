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
    public class AssetLegPriceResetFloatRateProduct : ProductBase, IHavePricingReference
    {
        private readonly SecurityBasket _basket;
        private readonly AssetLegResetFloatRate _rateLegReset;
        private double[] _lastFixing;
        private DateTime _lastFixingDate;

        private double _currentBaskValue;
        private double _currentFixing;

        private double _currentSpread;
        private Type _stockType;
        private Type _rateType;
        private double _quotity;
        private bool _firstFix;

        public bool ForceMid { get; set; }
        public string Reference
        {
            set
            {
                if ((value == _rateLegReset.PayerParty) && !ForceMid)
                {
                    _stockType = typeof(BidQuote);
                    _rateType = typeof(AskQuote);
                }
                else if ((value == _rateLegReset.ReceiverParty) && !ForceMid)
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

        public AssetLegPriceResetFloatRateProduct(AssetLegResetFloatRate rateLegReset)
            : base(rateLegReset.Id, rateLegReset.Start, rateLegReset.End)
        {
            _rateLegReset = rateLegReset;

            _basket = rateLegReset.Underlying as SecurityBasket; // TODO CHECK TYPE

            AddCurrency(rateLegReset.Currency);

            AddParty(rateLegReset.PayerParty);
            AddParty(rateLegReset.ReceiverParty);

            AddCallBack(rateLegReset, AssetPaymentLeg, -1);
            AddCallBack(rateLegReset.AllDates.Take(rateLegReset.Count), Fixing, -1);

            AddCallBack(rateLegReset.ResetShedule, Reset, -1);

            var refAsOis = rateLegReset.FixingReference as OisReference;
            if (refAsOis != null)
            {
                refAsOis.Tenor = rateLegReset.Period;
            }

            _stockType = typeof(MidQuote);
            _rateType = typeof(MidQuote);
            _firstFix = true;

            Reset();
        }

        private CallBackOutput Fixing(CallBackArg arg)
        {
            var m = arg.Model as IAffinePriceModel;
            _lastFixing = m.StockValues(_stockType);
            _lastFixingDate = arg.CurrentDate;

            var comps = _basket.Components;
            _currentBaskValue = 0d;
            for (int i = 0; i < comps.Count; i++)
            {
                var ccyStock = comps[i].Underlying.Currency.Code;
                var ccyPair = Tuple.Create(ccyStock, _rateLegReset.Currency.Code);
                _currentBaskValue += comps[i].Weight * _lastFixing[i] * m.FxValue(ccyPair, typeof(MidQuote));
            }

            _currentFixing = _rateLegReset.FixingReference.Fixing(m, _rateType);

            if (_firstFix)
            {
                if (_rateLegReset.Quotity.HasValue)
                {
                    _quotity = _rateLegReset.Quotity.Value;
                }
                else
                {
                    _quotity = _rateLegReset.Notional.Value / _currentBaskValue;
                }
                _firstFix = false;
            }

            return CallBackOutput.EmptyPaymentOutput();
        }

        private CallBackOutput Reset(CallBackArg arg)
        {
            var m = arg.Model as IAffinePriceModel;
            var stocks = m.StockValues(_stockType);

            var assetLeg = m.ComputeAssetLeg(m.CurrentDate, stocks);
            var fltRateLeg = m.ComputeFloatRateLeg(m.CurrentDate, stocks);

            double currentTRSPrice = (assetLeg - fltRateLeg) * _quotity;

            if (Math.Abs(currentTRSPrice) > _rateLegReset.Threshold)
            {
                var pay = AssetPaymentLeg(arg);
                Fixing(arg);
                return pay;
            }
            return CallBackOutput.EmptyPaymentOutput();
        }

        private CallBackOutput AssetPaymentLeg(CallBackArg arg)
        {
            var m = arg.Model as IAffinePriceModel;
            var output = new CallBackOutput(1);

            double period = _rateLegReset.DayCountConvention.Count(_lastFixingDate, arg.CurrentDate);
            double payoff = period * _currentFixing * _currentBaskValue * _quotity;

            output.AddPayment(_rateLegReset.PayerParty, _rateLegReset.ReceiverParty, _rateLegReset.Currency.Code, payoff, "RateLeg", false);

            return output;
        }

    }
}
