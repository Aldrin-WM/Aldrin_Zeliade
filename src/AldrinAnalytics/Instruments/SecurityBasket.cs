using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zeliade.Common;
using Zeliade.Finance.Common.Product;

namespace AldrinAnalytics.Instruments
{
   
    public class BasketComponent
    {
        public SingleNameTicker Underlying { get; set; }
        public double Weight { get; set; }

    }

    public class SecurityBasket : Ticker, IEnumerable<BasketComponent>, IDependOnSymbol
    {

        private readonly Dictionary<SingleNameTicker, BasketComponent> _content;
        private readonly HashSet<SingleNameTicker> _tickers;
        private readonly List<BasketComponent> _components;
        private double _sumWeights;
        public double SumWeights { get { return _sumWeights; } }

        public SecurityBasket(string name, string refCcy)
            : base(name, refCcy)
        {
            _content = new Dictionary<SingleNameTicker, BasketComponent>();
            _tickers = new HashSet<SingleNameTicker>();
            _components = new List<BasketComponent>();
            _sumWeights = 0d;
        }

        public ReadOnlyCollection<SingleNameTicker> Content
        {
            get { return _tickers.ToList().AsReadOnly(); }
        }

        public ReadOnlyCollection<BasketComponent> Components
        {
            get { return _components.AsReadOnly(); }
        }

        public ReadOnlyCollection<Currency> StockCurrencies
        {
            get { return Content.Select(x => x.ReferenceCurrency).Distinct().ToList().AsReadOnly(); }
        }

        public ReadOnlyCollection<CurrencyPair> DivToStock
        {
            get 
            {
                return Content.Select(x => new CurrencyPair(x.ReferenceCurrency.Code, x.DividendCurrency.Code))
                    .Distinct()
                    .ToList()
                    .AsReadOnly(); 
            }
        }

        public ReadOnlyCollection<CurrencyPair> StockToBasket
        {
            get
            {
                return Content.Select(x => new CurrencyPair(Currency.Code, x.ReferenceCurrency.Code))
                    .Distinct()
                    .ToList()
                    .AsReadOnly();
            }
        }

        public BasketComponent GetComponent(SingleNameTicker t)
        {
            Require.ArgumentNotNull(t, "t");
            BasketComponent output = null;
            if (!_content.TryGetValue(t, out output))
            {
                throw new KeyNotFoundException(string.Format("The ticker {0} is missing from the basket {1} !", t.Name, Name));
            }

            return output;

        }

        public SecurityBasket AddComponent(BasketComponent component)
        {
            Require.ArgumentNotNull(component, "component");
            if (!_content.ContainsKey(component.Underlying))
            {
                _content.Add(component.Underlying, component);
                _sumWeights += component.Weight;
                _tickers.Add(component.Underlying);
                _components.Add(component);
            }
            else
            {
                //_content[ticker] = component;
                throw new ArgumentException(string.Format("The component {0} is already registered in the basket {1} !", component.Underlying, Name));
            }
            return this;
        }

        public IEnumerator<BasketComponent> GetEnumerator()
        {
            return _content.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool DependOn(Symbol s)
        {
            var sn = s as SingleNameTicker;
            if (sn == null)
                return false;
            return _tickers.Contains(sn);
        }

        
    }
}
