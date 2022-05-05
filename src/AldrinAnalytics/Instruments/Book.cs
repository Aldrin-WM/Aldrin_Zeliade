using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Instruments
{
    public class Book : IEnumerable<Tuple<IAssetProduct, ICollateralScheme>>
    {
        private const string XllName = "Book";

        private readonly Dictionary<string, IAssetProduct> _book;
        private readonly Dictionary<string, ICollateralScheme> _collat;
        public string Id { get; private set; }

        [WorksheetFunction(XllName + ".Instruments")]
        public string[] Instruments { get { return _book.Keys.ToArray(); } }

        [WorksheetFunction(XllName + ".New")]
        public Book(string id)
        {
            Id = Require.ArgumentNotNullOrEmpty(id, "id");
            _book = new Dictionary<string, IAssetProduct>();
            _collat = new Dictionary<string, ICollateralScheme>();
        }

        [WorksheetFunction(XllName + ".AddInstrument")]
        public Book AddInstrument(IAssetProduct product, ICollateralScheme collat)
        {
            Require.ArgumentNotNull(product, "product");

            if (_book.ContainsKey(product.Id))
            {
                throw new ArgumentException(string.Format("The instrument {0} is already registered in the book {1}", product.Id, Id));
            }
            _book.Add(product.Id, product);
            _collat.Add(product.Id, collat);

            return this;
        }

        public bool Contains(string id)
        {
            return _book.ContainsKey(id);
        }

        public IAssetProduct Get(string id)
        {
            if (!_book.ContainsKey(id))
            {
                throw new ArgumentException(string.Format("The instrument {0} is not available !", id));
            }
            return _book[id];
        }

        public ICollateralScheme GetCollat(string id)
        {
            if (!_collat.ContainsKey(id))
            {
                throw new ArgumentException(string.Format("The instrument {0} is not available !", id));
            }
            return _collat[id];
        }

        public bool TryGet(string id, out IAssetProduct p)
        {
            return _book.TryGetValue(id, out p);
        }

        public bool TryGetCollat(string id, out ICollateralScheme c)
        {
            return _collat.TryGetValue(id, out c);
        }

        public IEnumerator<Tuple<IAssetProduct, ICollateralScheme>> GetEnumerator()
        {
            var l = new List<Tuple<IAssetProduct, ICollateralScheme>>();
            foreach (var item in _book)
            {
                var t = Tuple.Create(item.Value, _collat[item.Key]);
                l.Add(t);
            }

            return l.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
