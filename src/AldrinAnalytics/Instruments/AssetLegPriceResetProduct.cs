using AldrinAnalytics.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Product;

namespace AldrinAnalytics.Instruments
{
    class AssetLegPriceResetProduct : ProductBase, IHavePricingReference
    {
        private readonly SecurityBasket _basket;
        private readonly AssetLegReset _assetLegReset;
        private double[] _lastFixing;
        private DateTime _lastFixingDate;
        private List<string> _listCCYs;
        private Dictionary<string, double> _lastFXRates;
        private double _initialQuotity;
        private double _quotity;
        private double _currentQuotity;
        private Type _stockType;
        private Type _divType;
        private bool _firstFix = true;
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

        public AssetLegPriceResetProduct(AssetLegReset assetLegReset)
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
            
            //To Avoid Issue with first fixing
            AddCallBack(assetLegReset.AllDates[0], Fixing, -1);
            AddCallBack(assetLegReset, Fixing, -1);

            AddCallBack(assetLegReset.AllDates[0], ResetInitializer, -1);
            AddCallBack(assetLegReset.ResetShedule, Reset, -1);

            _stockType = typeof(MidQuote);
            _divType = typeof(MidQuote);

            Reset();
            _firstFix = true;
        }

        private CallBackOutput Fixing(CallBackArg arg)
        {
            //Before
            //var m = arg.Model as IJointModel;
            //_lastFixing = m.StockValues(_stockType);
            //_lastFixingDate = arg.CurrentDate;

            //return CallBackOutput.EmptyPaymentOutput();


            //Modification
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
                var m = arg.Model as IAffinePriceModel;

                var comps = _basket.Components;
                var currentBaskValue = 0d;
                for (int i = 0; i < comps.Count; i++)
                {
                    var ccyStock = comps[i].Underlying.Currency.Code;
                    var ccyPair = Tuple.Create(ccyStock, _assetLegReset.Currency.Code);
                    currentBaskValue += comps[i].Weight * _lastFixing[i] * m.FxValue(ccyPair, typeof(MidQuote));
                }
                if (_assetLegReset.Quotity.HasValue)
                {
                    _quotity = _assetLegReset.Quotity.Value;
                    _notional = _quotity * currentBaskValue;
                }
                else
                {
                    _quotity = _assetLegReset.Notional.Value / currentBaskValue;
                    _notional = _assetLegReset.Notional.Value;
                }
                //_currentQuotity = _initialQuotity;
                _firstFix = false;
            }

            return CallBackOutput.EmptyPaymentOutput();
        }


        protected override void InternalReset()
        {

        }

        private CallBackOutput Reset(CallBackArg arg)
        {
            var m = arg.Model as IAffinePriceModel;
            var stocks = m.StockValues(_stockType);

            var assetLeg = m.ComputeAssetLeg(m.CurrentDate, stocks);
            var fltRateLeg = m.ComputeFloatRateLeg(m.CurrentDate, stocks);

            double currentTRSPrice = (assetLeg - fltRateLeg) * _quotity;

            if (Math.Abs(currentTRSPrice) > _assetLegReset.Threshold)
            {
                var pay = AssetPaymentLeg(arg);
                Fixing(arg);
                return pay;
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

            payoff *= _quotity;

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
            payoffDiv *= _quotity;

            output.AddPayment(_assetLegReset.PayerParty, _assetLegReset.ReceiverParty, _assetLegReset.Currency.Code, payoffDiv, "Dividends", false);

            return output;



        }
    }
}
