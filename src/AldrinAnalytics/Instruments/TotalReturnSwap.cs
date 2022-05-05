using System;
using Zeliade.Common;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Mrc;
using System.Collections.ObjectModel;
using System.Collections.Generic;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Instruments
{
    public class TotalReturnSwap : IAssetProduct
    {
        private const string XllName = "TotalReturnSwap";

        public string Id { get; private set; }

        public AssetLegBase Leg1 { get; private set; }
        public AssetLegBase Leg2 { get; private set; }

        public ReadOnlyCollection<IAssetLeg> Legs
        {
            get
            {
                return new List<IAssetLeg>() { Leg1, Leg2 }.AsReadOnly();
            }
        }        

        [WorksheetFunction(XllName + ".New")]
        public TotalReturnSwap(string id, AssetLegBase leg1, AssetLegBase leg2)
        {
            Id = Require.ArgumentNotNullOrEmpty(id, "id");
            Leg1 = Require.ArgumentNotNull(leg1, "leg1");
            Leg2 = Require.ArgumentNotNull(leg2, "leg2");

            Require.Argument(leg1.PayerParty == leg2.ReceiverParty, "leg2", Error.Msg("The payer of leg1 is {0} while receiver of leg2 is {1} : should be the same entity !", leg1.PayerParty, leg2.ReceiverParty));
            Require.Argument(leg1.ReceiverParty == leg2.PayerParty, "leg2", Error.Msg("The receiver of leg1 is {0} while payer of leg2 is {1} : should be the same entity !", leg1.ReceiverParty, leg2.PayerParty));

        }

        public IProduct ToProduct(DateTime asof, bool forceMid, string pricingRef)
        {
            var p1 = Leg1.ToProduct(asof, forceMid, pricingRef);
            var p2 = Leg2.ToProduct(asof, forceMid, pricingRef);
            var ptf = new Portfolio(Id, new IProduct[] { p1, p2 });
            return ptf;
        }
    }
}
