using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade {
    class SwordMode : BladeMode {
        public override bool Test(Shatterblade sword) => false;
        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            sword.ReformParts();
        }

        public override void Update() {
            base.Update();
            if (sword.locking) {
                sword.handleAnnotationA.SetText("Hold A/X to expand\nthe blade");
                sword.gunShardAnnotation.SetText("Grab this shard to\nmake a handgun!");
                if (sword.item.handlers.Count() == 1) {
                    sword.otherHandAnnotation.SetTarget(sword.item.mainHandler.otherHand.transform);
                    

                    sword.otherHandAnnotation.offset
                        = new Vector3(-1, (sword.item.mainHandler.otherHand.side == Side.Right) ? 1 : -1, 0);
                    if (sword.item.mainHandler.otherHand.caster.spellInstance is SpellCastCharge) {
                        if (sword.item.mainHandler.otherHand.caster.spellInstance is SpellCastLightning) {
                            sword.otherHandAnnotation.SetText("Arc Cannon (Lightning spell) selected!\nLook back at the blade");
                            sword.imbueHandleAnnotation.SetText("Grab this shard for Arc Cannon mode");
                        } else if (sword.item.mainHandler.otherHand.caster.spellInstance is SpellCastProjectile) {
                            sword.otherHandAnnotation.SetText("Flamethrower (Fire spell) selected!\nLook back at the blade");
                            sword.imbueHandleAnnotation.SetText("Grab this shard for Flamethrower mode");
                        } else if (sword.item.mainHandler.otherHand.caster.spellInstance is SpellCastGravity) {
                            sword.otherHandAnnotation.SetText("Gravity Gun (Gravity spell) selected!\nLook back at the blade");
                            sword.imbueHandleAnnotation.SetText("Grab this shard for Gravity Gun mode");
                        }
                    } else {
                        sword.imbueHandleAnnotation.Hide();
                        sword.otherHandAnnotation.SetText("Select Fire, Lightning, or Gravity with\nthis hand to form a weapon");
                    }
                } else {
                    sword.otherHandAnnotation.Hide();
                }
            } else {
                sword.handleAnnotationA.Hide();
            }
            sword.handleAnnotationB.SetText($"Tap A/X to {(sword.locking ? "Shatter" : "Reform")}\n the blade");
        }
    }
}
