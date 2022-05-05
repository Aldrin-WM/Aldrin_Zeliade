using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using System;
using System.Collections.Generic;
using System.IO;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Pricers
{
    public class ForwardBasket : IForwardCurve
#if LOGGING
    , IDisposable
#endif 
    {
        private readonly SecurityBasket _basket;
        private readonly Dictionary<SingleNameTicker, double> _equityMarket;
        private readonly Dictionary<Currency, IDiscountCurve<DateTime>> _disc;
        private readonly IDividendCurve _divs;
        private readonly IRepoCurve _repo;
        private readonly Dictionary<Tuple<string, string>, IForwardForexCurve> _fx;
        private readonly IJointModelSingleQuoteType _histoModel; 

#if LOGGING
        private readonly StreamWriter _writer;
#endif

        public ForwardBasket(SecurityBasket basket
            , Dictionary<SingleNameTicker, double> equityMarket
            , Dictionary<Currency, IDiscountCurve<DateTime>> disc
            , IDividendCurve divs
            , IRepoCurve repo
            , Dictionary<Tuple<string, string>, IForwardForexCurve> fx
            , IJointModelSingleQuoteType histoModel)
        {
            _basket = basket ?? throw new ArgumentNullException(nameof(basket));
            _equityMarket = equityMarket ?? throw new ArgumentNullException(nameof(equityMarket));
            _disc = disc ?? throw new ArgumentNullException(nameof(disc));
            _divs = divs ?? throw new ArgumentNullException(nameof(divs));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _fx = fx ?? throw new ArgumentNullException(nameof(fx));
            _histoModel = histoModel; // null value if no histo required.

#if LOGGING
            var now = LogPath.StartTime;
            _writer = new StreamWriter(string.Format("{0}\\fwd_{1}_{2}.csv", LogPath.Value, now, GetHashCode()));
            _writer.WriteLine("Date;Basket;StockComp;DivComp;Ticker;Weight;RepoZc;DiscountZc;Fx");
#endif
        }

        public Ticker Underlying { get { return _basket; } }

        public double Spot
        {
            get
            {
                double output = 0d;
                foreach (var item in _basket.Content)
                {
                    var s = _basket.GetComponent(item);
                    var fxCurve = _fx[Tuple.Create(s.Underlying.ReferenceCurrency.Code, _basket.ReferenceCurrency.Code)];
                    output += s.Weight * fxCurve.Spot * _equityMarket[s.Underlying];
                }
                return output;
            }
        }

        public DateTime CurveDate { get { return _divs.MarketDate; } }

#if LOGGING
        public void Dispose()
        {

            _writer.Close();
        }
#endif
        public double Forward(DateTime d)
        {
            if (d < CurveDate)
                return HistoFixing(d);


            double output = 0d;

            var repoZc = _repo.ZcPrice(d);
            
            foreach (var item in _basket.Content)
            {
                var s = _basket.GetComponent(item);

                var fxRate = 1.0;
                if (item.ReferenceCurrency.Code != _basket.ReferenceCurrency.Code)
                {
                    var fxCurve = _fx[Tuple.Create(item.ReferenceCurrency.Code, _basket.ReferenceCurrency.Code)];
                    fxRate = fxCurve.Forward(d);
                }

                var discountZc = _disc[item.Currency].ZcPrice(d);
                var equitySpot = _equityMarket[item];
                var stockComponent = s.Weight * fxRate * equitySpot * repoZc / discountZc;
                output += stockComponent;

#if LOGGING
                _writer.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8}"
                    , d, _basket.Name, stockComponent, 0d, s.Underlying.Name, s.Weight, repoZc, discountZc, fxRate);
#endif
            }

            var allInBasket = _divs.AllInDividends(CurveDate, d);

            SecurityBasket firstBasket = null;

            foreach (var item in allInBasket)
            {
                // Weight
                double weight = 0.0;
                var snTicker = item.Ticker as SingleNameTicker;
                if (snTicker != null)
                {
                    if (_basket.Content.Contains(snTicker))
                        weight = _basket.GetComponent(snTicker).Weight;
                    else
                        continue;
                }
                else
                {
                    //weight = _basket.SumWeights / _basket.Content.Count;
                    weight = 1;

                    // TODO: voir si on introduit un controle des proxy div.
                    //var b = item.Ticker as SecurityBasket;
                    //if (b==null)
                    //{ throw (new ArgumentException("TODO")); }
                    //else 
                    //{
                    //    if(firstBasket==null)
                    //    { firstBasket = b; }
                    //    else if (!firstBasket.Equals(b))
                    //    { 
                    //    throw(new ArgumentException("TODO"));
                    //    }
                    //}
                }

                var discountZcMat = _disc[item.PaymentCurrency].ZcPrice(d);
                var discountZc1 = _disc[item.PaymentCurrency].ZcPrice(item.PaymentDate);

                var discountRepo = _repo.ZcPrice(item.PaymentDate);

                var fxRate = 1.0;
                if (item.PaymentCurrency.Code != _basket.ReferenceCurrency.Code)
                {
                    var fxCurve = _fx[Tuple.Create(item.PaymentCurrency.Code, _basket.ReferenceCurrency.Code)];
                    fxRate = fxCurve.Forward(item.PaymentDate);
                }

                var divImpact = weight * (discountZc1 / discountRepo) / (discountZcMat / repoZc) * fxRate * item.GrossAmount * item.AllIn;

#if LOGGING
                _writer.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8}", d, _basket.Name, 0d, divImpact, item.Ticker.Name, weight, discountRepo, discountZc1, fxRate);
#endif
                output -= divImpact;
            }

#if LOGGING
            _writer.Flush();
#endif
            return output;
        }


        private double HistoFixing(DateTime d)
        {
            _histoModel.CurrentDate = d;
            double output = 0d;

            var stocks = _histoModel.StockValues(); // output dans l'ordre d'enumeration de basket.
            int indexStock = 0;
            foreach (var item in _basket.Content)
            {
                var s = _basket.GetComponent(item);

                var fxRate = 1.0;
                if (item.ReferenceCurrency.Code != _basket.ReferenceCurrency.Code)
                {
                    var key = Tuple.Create(item.ReferenceCurrency.Code, _basket.ReferenceCurrency.Code);
                    fxRate = _histoModel.FxValue(key);
                }

                var equitySpot = stocks[indexStock];
                var stockComponent = s.Weight * fxRate * equitySpot;
                output += stockComponent;
                ++indexStock;

#if LOGGING
                _writer.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8}"
                    , d, _basket.Name, stockComponent, 0d, s.Underlying.Name, s.Weight, repoZc, discountZc, fxRate);
#endif
            }

#if LOGGING
            _writer.Flush();
#endif
            return output;
        }

    }
}
