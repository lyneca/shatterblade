using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using Newtonsoft.Json;
using Shatterblade.Modes;

namespace Shatterblade.Modes {
    /// <summary>
    /// SpellMode: Modes triggered by grabbing a specific shard with a spell equipped.
    /// </summary>
    /// <typeparam name="T">The SpellCastCharge that is needed to trigger the functionality</typeparam>
    public abstract class SpellMode<T> : GrabbedShardMode where T: SpellCastCharge {

        /// <summary>
        /// The part that triggers the functionality, when grabbed with a hand with the spell T equipped
        /// </summary>
        /// <returns></returns>
        public override int TargetPartNum() => 11;

        /// <returns>The spell instance (T) in the hand that is holding the shard</returns>
        public T Spell() => Hand().caster.spellInstance as T;

        /// <returns>True if the target shard is being held with a hand that has the spell T equipped.</returns>
        public override bool Test(Shatterblade sword)
            => sword.GetPart(TargetPartNum())?.item?.mainHandler?.caster.spellInstance is T;
    }
}
